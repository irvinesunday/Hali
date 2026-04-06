// apps/citizen-mobile/app/(app)/settings/account.tsx
//
// Account settings — minimal profile view + nav into the other settings
// screens + sign out.
//
// Phone number is masked to its last 4 digits before display so the full
// number doesn't appear on screen casually. Email + display name are
// rendered as-is from /v1/users/me when present.

import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useQuery } from '@tanstack/react-query';
import { getMe } from '../../../src/api/users';
import { useAuth } from '../../../src/context/AuthContext';
import { Loading } from '../../../src/components/common/Loading';
import { formatRelativeTime } from '../../../src/utils/formatters';

/**
 * Mask all but the last 4 digits of an E.164 phone number.
 *   "+254712345678" → "+254 ••• ••5678"
 * Returns the input unchanged if it doesn't look like an E.164 number.
 */
function maskPhone(e164: string | null): string {
  if (e164 === null || e164.length < 6) return '—';
  const last4 = e164.slice(-4);
  return `${e164.slice(0, 4)} ••• ••${last4}`;
}

export default function AccountSettingsScreen(): React.ReactElement {
  const router = useRouter();
  const { signOut } = useAuth();
  const [signingOut, setSigningOut] = useState(false);

  const meQuery = useQuery({
    queryKey: ['users', 'me'],
    queryFn: async () => {
      const result = await getMe();
      if (!result.ok) throw new Error(result.error.message);
      return result.value;
    },
  });

  function handleLogout(): void {
    Alert.alert('Sign out', 'Are you sure you want to sign out?', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Sign out',
        style: 'destructive',
        onPress: async () => {
          setSigningOut(true);
          try {
            await signOut();
            router.replace('/(auth)/phone');
          } catch {
            // signOut is best-effort and clears local state regardless;
            // ensure we still navigate away even on a transport error.
            router.replace('/(auth)/phone');
          } finally {
            setSigningOut(false);
          }
        },
      },
    ]);
  }

  if (meQuery.isLoading && !meQuery.data) return <Loading />;

  const me = meQuery.data;

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
          <Ionicons name="arrow-back" size={24} color="#111827" />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Account</Text>
        <View style={styles.navSpacer} />
      </View>

      <ScrollView contentContainerStyle={styles.content}>
        {me !== undefined && (
          <View style={styles.card}>
            <InfoRow label="Phone" value={maskPhone(me.phoneE164)} />
            <InfoRow label="Email" value={me.email ?? '—'} />
            <InfoRow label="Status" value={me.status} />
            <InfoRow
              label="Member since"
              value={formatRelativeTime(me.createdAt)}
            />
          </View>
        )}

        <View style={styles.nav}>
          <NavItem
            label="Ward following"
            icon="location-outline"
            onPress={() => router.push('/(app)/settings/wards')}
          />
          <NavItem
            label="Notifications"
            icon="notifications-outline"
            onPress={() => router.push('/(app)/settings/notifications')}
          />
        </View>

        <TouchableOpacity
          style={[styles.signOutBtn, signingOut && styles.signOutBtnBusy]}
          onPress={handleLogout}
          disabled={signingOut}
          accessible
          accessibilityRole="button"
          accessibilityLabel="Sign out"
          accessibilityState={{ busy: signingOut }}
        >
          {signingOut ? (
            <ActivityIndicator color="#1a3a2f" size="small" />
          ) : (
            <Text style={styles.signOutBtnText}>Sign out</Text>
          )}
        </TouchableOpacity>

        <Text style={styles.version}>Hali v1.0.0</Text>
      </ScrollView>
    </SafeAreaView>
  );
}

function InfoRow({
  label,
  value,
}: {
  label: string;
  value: string;
}): React.ReactElement {
  return (
    <View style={styles.infoRow}>
      <Text style={styles.infoLabel}>{label}</Text>
      <Text style={styles.infoValue} numberOfLines={1}>
        {value}
      </Text>
    </View>
  );
}

function NavItem({
  label,
  icon,
  onPress,
}: {
  label: string;
  icon: React.ComponentProps<typeof Ionicons>['name'];
  onPress: () => void;
}): React.ReactElement {
  return (
    <TouchableOpacity
      style={styles.navItem}
      onPress={onPress}
      activeOpacity={0.7}
      accessible
      accessibilityRole="button"
      accessibilityLabel={label}
    >
      <Ionicons name={icon} size={20} color="#374151" />
      <Text style={styles.navItemLabel}>{label}</Text>
      <Ionicons name="chevron-forward" size={16} color="#9CA3AF" />
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#F9FAFB' },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    backgroundColor: '#FFFFFF',
    borderBottomWidth: 1,
    borderBottomColor: '#E5E7EB',
  },
  navTitle: { fontSize: 17, fontWeight: '600', color: '#111827' },
  navSpacer: { width: 24 },
  content: { padding: 20, gap: 16, paddingBottom: 40 },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    paddingHorizontal: 16,
    borderWidth: 1,
    borderColor: '#E5E7EB',
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 14,
    borderBottomWidth: 1,
    borderBottomColor: '#F3F4F6',
    gap: 16,
  },
  infoLabel: { fontSize: 14, color: '#6B7280' },
  infoValue: {
    fontSize: 14,
    color: '#111827',
    fontWeight: '500',
    flex: 1,
    textAlign: 'right',
  },
  nav: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    borderWidth: 1,
    borderColor: '#E5E7EB',
    overflow: 'hidden',
  },
  navItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    paddingVertical: 14,
    paddingHorizontal: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#F3F4F6',
  },
  navItemLabel: { flex: 1, fontSize: 15, color: '#111827' },
  signOutBtn: {
    marginTop: 8,
    backgroundColor: '#FFFFFF',
    borderWidth: 1.5,
    borderColor: '#1a3a2f',
    borderRadius: 10,
    paddingVertical: 14,
    alignItems: 'center',
  },
  signOutBtnBusy: { opacity: 0.7 },
  signOutBtnText: { color: '#1a3a2f', fontSize: 15, fontWeight: '600' },
  version: { fontSize: 12, color: '#9CA3AF', textAlign: 'center' },
});
