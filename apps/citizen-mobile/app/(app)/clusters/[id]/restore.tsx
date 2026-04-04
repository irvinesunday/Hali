// Restoration Prompt — Flow E
import React from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Alert,
} from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import * as Crypto from 'expo-crypto';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import { useRestorationResponse } from '../../../../src/hooks/useClusters';
import { PARTICIPATION_LABELS } from '../../../../src/config/constants';

export default function RestorationPromptScreen() {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const restorationMutation = useRestorationResponse(id);

  async function getDeviceHash(): Promise<string> {
    const raw = `${Device.osName}-${Device.osVersion}-${Device.modelName}-${Application.applicationId}`;
    return Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, raw);
  }

  async function handleRespond(
    response: 'restoration_yes' | 'restoration_no' | 'restoration_unsure',
  ) {
    const deviceHash = await getDeviceHash();
    restorationMutation.mutate(
      { response, deviceHash },
      {
        onSuccess: () => {
          router.back();
        },
        onError: () => {
          Alert.alert('Error', 'Could not record your response. Please try again.');
        },
      },
    );
  }

  const OPTIONS: Array<{
    key: 'restoration_yes' | 'restoration_no' | 'restoration_unsure';
    label: string;
    color: string;
    bg: string;
  }> = [
    {
      key: 'restoration_yes',
      label: PARTICIPATION_LABELS.restoration_yes,
      color: '#166534',
      bg: '#f0fdf4',
    },
    {
      key: 'restoration_no',
      label: PARTICIPATION_LABELS.restoration_no,
      color: '#991b1b',
      bg: '#fef2f2',
    },
    {
      key: 'restoration_unsure',
      label: PARTICIPATION_LABELS.restoration_unsure,
      color: '#374151',
      bg: '#f9fafb',
    },
  ];

  return (
    <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
      <View style={styles.navBar}>
        <TouchableOpacity onPress={() => router.back()} hitSlop={12}>
          <Ionicons name="close" size={24} color="#111827" />
        </TouchableOpacity>
      </View>

      <View style={styles.content}>
        <Text style={styles.heading}>Has the situation been resolved?</Text>
        <Text style={styles.sub}>
          Your response helps confirm whether this issue has been fixed.
        </Text>

        <View style={styles.options}>
          {OPTIONS.map((opt) => (
            <TouchableOpacity
              key={opt.key}
              style={[styles.option, { backgroundColor: opt.bg }]}
              activeOpacity={0.75}
              disabled={restorationMutation.isPending}
              onPress={() => handleRespond(opt.key)}
            >
              <Text style={[styles.optionLabel, { color: opt.color }]}>
                {opt.label}
              </Text>
              <Ionicons name="chevron-forward" size={18} color={opt.color} />
            </TouchableOpacity>
          ))}
        </View>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#fff' },
  navBar: {
    padding: 16,
    alignItems: 'flex-end',
  },
  content: { flex: 1, padding: 24, gap: 20 },
  heading: { fontSize: 24, fontWeight: '700', color: '#111827' },
  sub: { fontSize: 15, color: '#6b7280', lineHeight: 22 },
  options: { gap: 12, marginTop: 8 },
  option: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    borderRadius: 12,
    padding: 18,
    borderWidth: 1,
    borderColor: '#e5e7eb',
  },
  optionLabel: { fontSize: 17, fontWeight: '600' },
});
