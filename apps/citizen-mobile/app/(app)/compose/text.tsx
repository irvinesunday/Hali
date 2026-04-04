// Signal Composer Step 1 — Free text input
import React, { useEffect } from 'react';
import { View, TouchableOpacity, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import * as Crypto from 'expo-crypto';
import { useMutation } from '@tanstack/react-query';
import { ComposerStep1 } from '../../../src/components/signals/ComposerStep1';
import { previewSignal } from '../../../src/api/signals';
import { useComposerContext } from '../../../src/context/ComposerContext';

export default function ComposerTextScreen() {
  const router = useRouter();
  const { setFreeText, setLocationHint, setPreview, setDeviceHash } =
    useComposerContext();

  useEffect(() => {
    async function initDeviceHash() {
      const raw = `${Device.osName}-${Device.osVersion}-${Device.modelName}-${Application.applicationId}`;
      const hash = await Crypto.digestStringAsync(
        Crypto.CryptoDigestAlgorithm.SHA256,
        raw,
      );
      setDeviceHash(hash);
    }
    initDeviceHash();
  }, [setDeviceHash]);

  const previewMutation = useMutation({
    mutationFn: ({
      freeText,
      locationHint,
    }: {
      freeText: string;
      locationHint?: string;
    }) =>
      previewSignal({
        freeText,
        selectedWard: locationHint,
        countryCode: 'KE',
      }),
    onSuccess: (data, variables) => {
      setFreeText(variables.freeText);
      setLocationHint(variables.locationHint);
      setPreview(data);
      router.push('/(app)/compose/confirm');
    },
  });

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity onPress={() => router.back()} hitSlop={12}>
          <Ionicons name="close" size={24} color="#111827" />
        </TouchableOpacity>
      </View>
      <ComposerStep1
        onPreview={(freeText, locationHint) =>
          previewMutation.mutate({ freeText, locationHint })
        }
        loading={previewMutation.isPending}
        error={
          previewMutation.isError
            ? 'Could not analyse your report. Please try again.'
            : null
        }
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#fff' },
  navBar: {
    paddingHorizontal: 16,
    paddingVertical: 10,
    alignItems: 'flex-end',
  },
});
