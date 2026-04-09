// apps/citizen-mobile/app/(app)/settings/notifications.tsx
//
// Notification preferences screen.
//
// Two sections:
//   1. Push enable/disable — re-trigger registration if the user denied
//      permission earlier and changed their mind. Initial registration
//      happens automatically after OTP verify (see useAutoPushRegistration
//      hook in (app)/_layout.tsx).
//   2. Per-type toggles — clusterActivated / restorationPrompt /
//      clusterResolved. PUT /v1/users/me/notification-settings on each
//      change. Optimistic update with a rollback on failure.

import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  Switch,
  ActivityIndicator,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { ArrowLeft } from 'lucide-react-native';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
} from '../../../src/theme';
import * as Notifications from 'expo-notifications';
import * as Crypto from 'expo-crypto';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getMe, updateNotificationSettings } from '../../../src/api/users';
import { registerPushToken } from '../../../src/api/devices';
import { Loading } from '../../../src/components/common/Loading';
import type { NotificationSettings } from '../../../src/types/api';

const DEFAULT_SETTINGS: NotificationSettings = {
  clusterActivated: true,
  restorationPrompt: true,
  clusterResolved: true,
};

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

export default function NotificationSettingsScreen(): React.ReactElement {
  const router = useRouter();
  const qc = useQueryClient();

  const [settings, setSettings] =
    useState<NotificationSettings>(DEFAULT_SETTINGS);
  const [pushBusy, setPushBusy] = useState(false);
  const [pushMessage, setPushMessage] = useState<string | null>(null);
  const [toggleError, setToggleError] = useState<string | null>(null);

  // ── Load current settings from /me ────────────────────────────────────────
  const meQuery = useQuery({
    queryKey: ['users', 'me'],
    queryFn: async () => {
      const result = await getMe();
      if (!result.ok) throw new Error(result.error.message);
      return result.value;
    },
  });

  useEffect(() => {
    if (meQuery.data?.notificationSettings) {
      setSettings(meQuery.data.notificationSettings);
    }
  }, [meQuery.data]);

  // ── Update settings mutation ──────────────────────────────────────────────
  // Optimistic toggle: rollback on error uses React Query's onMutate context.
  const updateMutation = useMutation<
    void,
    Error,
    NotificationSettings,
    { previous: NotificationSettings }
  >({
    mutationFn: async (next) => {
      const result = await updateNotificationSettings(next);
      if (!result.ok) throw new Error(result.error.message);
    },
    onMutate: (next) => {
      const previous = settings;
      setSettings(next);
      return { previous };
    },
    onError: (_err, _variables, context) => {
      if (context?.previous !== undefined) {
        setSettings(context.previous);
      }
      setToggleError(
        'Could not save your preference. Please try again.',
      );
    },
    onSuccess: () => {
      setToggleError(null);
      qc.invalidateQueries({ queryKey: ['users', 'me'] });
    },
  });

  function handleToggle(key: keyof NotificationSettings): void {
    const next: NotificationSettings = { ...settings, [key]: !settings[key] };
    updateMutation.mutate(next);
  }

  // ── Re-register push (for users who initially denied) ─────────────────────
  async function handleEnablePush(): Promise<void> {
    setPushBusy(true);
    setPushMessage(null);

    try {
      const { status } = await Notifications.requestPermissionsAsync();
      if (status !== 'granted') {
        setPushMessage(
          'Permission denied. Enable notifications in your device settings to receive alerts.',
        );
        return;
      }
      const tokenResult = await Notifications.getExpoPushTokenAsync();
      const deviceHash = await getDeviceHash();
      const result = await registerPushToken({
        expoPushToken: tokenResult.data,
        deviceHash,
      });

      if (result.ok) {
        setPushMessage('Push notifications enabled.');
      } else {
        setPushMessage(
          result.error.message ||
            'Could not register for push notifications.',
        );
      }
    } catch {
      setPushMessage('Could not register for push notifications.');
    } finally {
      setPushBusy(false);
    }
  }

  if (meQuery.isLoading && !meQuery.data) return <Loading />;

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity
          onPress={() => router.back()}
          hitSlop={12}
          accessible
          accessibilityRole="button"
          accessibilityLabel="Back"
        >
          <ArrowLeft size={24} color={Colors.foreground} />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Notifications</Text>
        <View style={styles.navSpacer} />
      </View>

      <ScrollView contentContainerStyle={styles.content}>
        {/* Push registration */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Push notifications</Text>
          <Text style={styles.sectionSub}>
            Push registration happens automatically when you sign in. If
            you denied permission and want to enable it now, tap below.
          </Text>
          <TouchableOpacity
            style={[styles.pushBtn, pushBusy && styles.pushBtnBusy]}
            onPress={() => void handleEnablePush()}
            disabled={pushBusy}
            accessible
            accessibilityRole="button"
            accessibilityLabel="Re-enable push notifications"
            accessibilityState={{ busy: pushBusy }}
          >
            {pushBusy ? (
              <ActivityIndicator color={Colors.primaryForeground} size="small" />
            ) : (
              <Text style={styles.pushBtnText}>Enable push notifications</Text>
            )}
          </TouchableOpacity>
          {pushMessage !== null && (
            <Text
              style={styles.pushMessage}
              accessible
              accessibilityRole="alert"
              accessibilityLiveRegion="polite"
            >
              {pushMessage}
            </Text>
          )}
        </View>

        {/* Per-type preferences */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Alert preferences</Text>
          <ToggleRow
            label="Cluster activated"
            description="When a cluster in a ward you follow becomes active"
            value={settings.clusterActivated}
            onToggle={() => handleToggle('clusterActivated')}
            disabled={updateMutation.isPending}
          />
          <ToggleRow
            label="Restoration prompt"
            description="When a cluster you marked yourself affected by may have been resolved"
            value={settings.restorationPrompt}
            onToggle={() => handleToggle('restorationPrompt')}
            disabled={updateMutation.isPending}
          />
          <ToggleRow
            label="Cluster resolved"
            description="When a cluster you participated in is fully resolved"
            value={settings.clusterResolved}
            onToggle={() => handleToggle('clusterResolved')}
            disabled={updateMutation.isPending}
          />
          {toggleError !== null && (
            <Text
              style={styles.toggleError}
              accessible
              accessibilityRole="alert"
            >
              {toggleError}
            </Text>
          )}
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

function ToggleRow({
  label,
  description,
  value,
  onToggle,
  disabled,
}: {
  label: string;
  description: string;
  value: boolean;
  onToggle: () => void;
  disabled: boolean;
}): React.ReactElement {
  return (
    <View style={styles.toggleRow}>
      <View style={styles.toggleText}>
        <Text style={styles.toggleLabel}>{label}</Text>
        <Text style={styles.toggleDesc}>{description}</Text>
      </View>
      <Switch
        value={value}
        onValueChange={onToggle}
        disabled={disabled}
        trackColor={{ false: Colors.border, true: Colors.primary }}
        thumbColor={Colors.primaryForeground}
        accessible
        accessibilityRole="switch"
        accessibilityLabel={label}
        accessibilityState={{ checked: value, disabled }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: Colors.card },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: Spacing.lg,
    paddingVertical: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
  },
  navTitle: {
    fontSize: FontSize.cardTitle,
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
  },
  navSpacer: { width: 24 },
  content: { padding: Spacing.xl, gap: Spacing['3xl'] - 4 },
  section: { gap: Spacing.md },
  sectionTitle: {
    fontSize: FontSize.cardTitle,
    fontFamily: FontFamily.bold,
    color: Colors.foreground,
  },
  sectionSub: {
    fontSize: FontSize.body,
    color: Colors.mutedForeground,
    lineHeight: 20,
  },
  pushBtn: {
    backgroundColor: Colors.primary,
    borderRadius: Radius.md,
    paddingVertical: Spacing.md,
    alignItems: 'center',
  },
  pushBtnBusy: { opacity: 0.7 },
  pushBtnText: {
    color: Colors.primaryForeground,
    fontSize: FontSize.cardTitle,
    fontFamily: FontFamily.semiBold,
  },
  pushMessage: {
    fontSize: FontSize.bodySmall,
    color: Colors.foreground,
  },
  toggleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
    gap: Spacing.md,
  },
  toggleText: { flex: 1, gap: 2 },
  toggleLabel: {
    fontSize: FontSize.cardTitle,
    color: Colors.foreground,
    fontFamily: FontFamily.medium,
  },
  toggleDesc: {
    fontSize: FontSize.bodySmall,
    color: Colors.mutedForeground,
    lineHeight: 18,
  },
  toggleError: {
    fontSize: FontSize.bodySmall,
    color: Colors.destructive,
    marginTop: Spacing.xs,
  },
});
