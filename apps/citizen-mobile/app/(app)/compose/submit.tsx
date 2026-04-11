// Signal Composer Step 3 — Join existing or create new
import React from 'react';
import { View, TouchableOpacity, StyleSheet, Text, Alert } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import * as Crypto from 'expo-crypto';
import * as Location from 'expo-location';
import { useMutation } from '@tanstack/react-query';
import { ComposerStep3 } from '../../../src/components/signals/ComposerStep3';
import { useComposerContext } from '../../../src/context/ComposerContext';
import { submitSignal } from '../../../src/api/signals';
import { Loading } from '../../../src/components/common/Loading';
import type { SignalSubmitRequest } from '../../../src/types/api';

export default function ComposerSubmitScreen() {
  const router = useRouter();
  const { freeText, preview, deviceHash, reset } = useComposerContext();

  const submitMutation = useMutation({
    mutationFn: async (_joinExisting: boolean) => {
      if (!preview) throw new Error('No preview');

      const { status } = await Location.requestForegroundPermissionsAsync();
      let position: Location.LocationObject | null = null;
      if (status === 'granted') {
        try {
          position = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
        } catch {
          position = await Location.getLastKnownPositionAsync();
        }
      } else {
        position = await Location.getLastKnownPositionAsync();
      }
      if (!position) throw new Error('Could not determine location. Please enable location services and try again.');

      const idempotencyKey = await Crypto.digestStringAsync(
        Crypto.CryptoDigestAlgorithm.SHA256,
        `${freeText}:${Date.now()}`,
      );

      const body: SignalSubmitRequest = {
        idempotencyKey,
        deviceHash,
        freeText,
        category: preview.category,
        subcategorySlug: preview.subcategorySlug,
        conditionSlug: preview.conditionSlug ?? undefined,
        conditionConfidence: preview.conditionConfidence,
        latitude: position.coords.latitude,
        longitude: position.coords.longitude,
        locationLabel: preview.location.locationLabel ?? undefined,
        locationPrecisionType:
          preview.location.locationPrecisionType ?? undefined,
        locationConfidence: preview.location.locationConfidence,
        locationSource: preview.location.locationSource,
        temporalType: preview.temporalType ?? undefined,
        neutralSummary: preview.neutralSummary ?? undefined,
      };

      return submitSignal(body);
    },
    onSuccess: (data) => {
      reset();
      router.replace(`/(app)/clusters/${data.clusterId}`);
    },
    onError: () => {
      Alert.alert(
        'Submission failed',
        'Could not submit your signal. Please try again.',
      );
    },
  });

  if (!preview) return <Loading message="Loading…" />;

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity onPress={() => router.back()} hitSlop={12}>
          <Ionicons name="arrow-back" size={24} color="#111827" />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Step 3 of 3</Text>
        <View style={{ width: 24 }} />
      </View>
      <ComposerStep3
        preview={preview}
        onSubmit={(joinExisting) => submitMutation.mutate(joinExisting)}
        loading={submitMutation.isPending}
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#fff' },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  navTitle: { fontSize: 14, color: '#6b7280' },
});
