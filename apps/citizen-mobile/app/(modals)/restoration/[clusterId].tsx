// apps/citizen-mobile/app/(modals)/restoration/[clusterId].tsx
//
// Restoration prompt — presented as a modal when a cluster transitions
// to possible_restoration and the current user previously voted Affected.
// Reads { clusterId } from route params.
//
// Wire values verified against ClustersController.RecordRestorationResponse:
//   "restored"       → records as ParticipationType.RestorationYes
//   "still_affected" → records as ParticipationType.Affected (re-affirms)
//   "not_sure"       → records as ParticipationType.RestorationUnsure
//
// There is no "no, not restored" wire value — the negative case is
// expressed as "still affected", which re-records the user as currently
// affected, undoing any prior restoration vote. This matches the CIVIS
// doctrine: restoration requires confirmation, not a binary tally.

import React, { useState } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
} from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import * as Crypto from 'expo-crypto';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import { submitRestorationResponse } from '../../../src/api/clusters';
import type { RestorationResponseValue } from '../../../src/types/api';

type ScreenState = 'idle' | 'loading' | 'error';

interface OptionConfig {
  key: RestorationResponseValue;
  label: string;
  helper: string;
  bg: string;
  borderColor: string;
  textColor: string;
}

const OPTIONS: readonly OptionConfig[] = [
  {
    key: 'restored',
    label: 'Service restored for me',
    helper: "I'm no longer affected — things are back to normal.",
    bg: '#F0FDF4',
    borderColor: '#86EFAC',
    textColor: '#166534',
  },
  {
    key: 'still_affected',
    label: 'Still affected',
    helper: 'The issue is ongoing for me.',
    bg: '#FEF2F2',
    borderColor: '#FCA5A5',
    textColor: '#991B1B',
  },
  {
    key: 'not_sure',
    label: 'Not sure yet',
    helper: 'I need a bit more time to confirm.',
    bg: '#F9FAFB',
    borderColor: '#E5E7EB',
    textColor: '#374151',
  },
];

async function getDeviceHash(): Promise<string> {
  const parts = [
    Device.modelName ?? 'unknown',
    Device.osName ?? 'unknown',
    Device.osVersion ?? 'unknown',
    Device.brand ?? 'unknown',
    Application.applicationId ?? 'unknown',
  ].join('|');
  return Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, parts);
}

export default function RestorationPromptModal(): React.ReactElement {
  const router = useRouter();
  const { clusterId } = useLocalSearchParams<{ clusterId: string }>();
  const [screenState, setScreenState] = useState<ScreenState>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const [pendingKey, setPendingKey] = useState<RestorationResponseValue | null>(
    null,
  );

  async function handleRespond(value: RestorationResponseValue): Promise<void> {
    if (screenState === 'loading' || !clusterId) return;
    setScreenState('loading');
    setErrorMessage('');
    setPendingKey(value);

    try {
      const deviceHash = await getDeviceHash();
      const result = await submitRestorationResponse(clusterId, {
        response: value,
        deviceHash,
      });

      if (result.ok) {
        // Pop the modal — the parent cluster screen will refetch on focus
        // and the new state will surface from there.
        router.back();
        return;
      }

      setScreenState('error');
      setPendingKey(null);
      if (result.error.code === 'invalid_restoration_response') {
        setErrorMessage(
          'That response is no longer valid. Please try again.',
        );
      } else if (result.error.status === 422) {
        setErrorMessage(
          result.error.message ||
            'Could not record your response. Please try again.',
        );
      } else {
        setErrorMessage(
          result.error.message || 'Something went wrong. Please try again.',
        );
      }
    } catch {
      setScreenState('error');
      setPendingKey(null);
      setErrorMessage('Something went wrong. Please try again.');
    }
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
      <View style={styles.navBar}>
        <TouchableOpacity
          onPress={() => router.back()}
          hitSlop={12}
          accessible
          accessibilityRole="button"
          accessibilityLabel="Close"
          disabled={screenState === 'loading'}
        >
          <Ionicons name="close" size={24} color="#111827" />
        </TouchableOpacity>
      </View>

      <View style={styles.content}>
        <Text style={styles.heading}>Has the situation been resolved?</Text>
        <Text style={styles.sub}>
          Your response helps confirm whether this issue has been fixed.
        </Text>

        <View style={styles.options}>
          {OPTIONS.map((opt) => {
            const isPending = pendingKey === opt.key;
            return (
              <TouchableOpacity
                key={opt.key}
                style={[
                  styles.option,
                  { backgroundColor: opt.bg, borderColor: opt.borderColor },
                ]}
                activeOpacity={0.75}
                disabled={screenState === 'loading'}
                onPress={() => handleRespond(opt.key)}
                accessible
                accessibilityRole="button"
                accessibilityLabel={opt.label}
                accessibilityHint={opt.helper}
                accessibilityState={{
                  disabled: screenState === 'loading',
                  busy: isPending,
                }}
              >
                <View style={styles.optionTextWrap}>
                  <Text style={[styles.optionLabel, { color: opt.textColor }]}>
                    {opt.label}
                  </Text>
                  <Text style={[styles.optionHelper, { color: opt.textColor }]}>
                    {opt.helper}
                  </Text>
                </View>
                {isPending ? (
                  <ActivityIndicator size="small" color={opt.textColor} />
                ) : (
                  <Ionicons
                    name="chevron-forward"
                    size={18}
                    color={opt.textColor}
                  />
                )}
              </TouchableOpacity>
            );
          })}
        </View>

        {screenState === 'error' && errorMessage !== '' && (
          <Text
            style={styles.error}
            accessible
            accessibilityRole="alert"
            accessibilityLiveRegion="polite"
          >
            {errorMessage}
          </Text>
        )}
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#FFFFFF' },
  navBar: { padding: 16, alignItems: 'flex-end' },
  content: { flex: 1, padding: 24, gap: 20 },
  heading: { fontSize: 24, fontWeight: '700', color: '#111827' },
  sub: { fontSize: 15, color: '#6B7280', lineHeight: 22 },
  options: { gap: 12, marginTop: 8 },
  option: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    borderRadius: 12,
    padding: 18,
    borderWidth: 1.5,
  },
  optionTextWrap: { flex: 1, gap: 4 },
  optionLabel: { fontSize: 17, fontWeight: '600' },
  optionHelper: { fontSize: 13, lineHeight: 18, opacity: 0.85 },
  error: {
    fontSize: 14,
    color: '#DC2626',
    textAlign: 'center',
    marginTop: 8,
  },
});
