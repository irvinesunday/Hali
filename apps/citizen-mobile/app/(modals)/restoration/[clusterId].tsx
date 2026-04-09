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
import { X, ChevronRight } from 'lucide-react-native';
import * as Crypto from 'expo-crypto';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
} from '../../../src/theme';
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
    bg: Colors.emeraldSubtle,
    borderColor: Colors.conditionBadge.emerald.border,
    textColor: Colors.emerald,
  },
  {
    key: 'still_affected',
    label: 'Still affected',
    helper: 'The issue is ongoing for me.',
    bg: Colors.destructiveSubtle,
    borderColor: Colors.conditionBadge.red.border,
    textColor: Colors.destructive,
  },
  {
    key: 'not_sure',
    label: 'Not sure yet',
    helper: 'I need a bit more time to confirm.',
    bg: Colors.muted,
    borderColor: Colors.border,
    textColor: Colors.foreground,
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
          <X size={24} color={Colors.foreground} />
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
                  <ChevronRight size={18} color={opt.textColor} />
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
  safe: { flex: 1, backgroundColor: Colors.card },
  navBar: { padding: Spacing.lg, alignItems: 'flex-end' },
  content: { flex: 1, padding: Spacing['2xl'], gap: Spacing.xl },
  heading: {
    fontSize: FontSize.appName,
    fontFamily: FontFamily.bold,
    color: Colors.foreground,
  },
  sub: {
    fontSize: FontSize.cardTitle,
    color: Colors.mutedForeground,
    lineHeight: 22,
  },
  options: { gap: Spacing.md, marginTop: Spacing.sm },
  option: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    borderRadius: Radius.lg,
    padding: Spacing.lg + 2,
    borderWidth: 1.5,
  },
  optionTextWrap: { flex: 1, gap: 4 },
  optionLabel: {
    fontSize: FontSize.cardTitle,
    fontFamily: FontFamily.semiBold,
  },
  optionHelper: {
    fontSize: FontSize.bodySmall,
    lineHeight: 18,
    opacity: 0.85,
  },
  error: {
    fontSize: FontSize.body,
    color: Colors.destructive,
    textAlign: 'center',
    marginTop: Spacing.sm,
  },
});
