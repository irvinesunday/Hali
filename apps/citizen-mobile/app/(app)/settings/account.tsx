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
import {
  ArrowLeft,
  ChevronRight,
  MapPin,
  Bell,
  type LucideIcon,
} from 'lucide-react-native';
import { useQuery } from '@tanstack/react-query';
import { getMe } from '../../../src/api/users';
import { useAuth } from '../../../src/context/AuthContext';
import { Loading } from '../../../src/components/common/Loading';
import { formatRelativeTime } from '../../../src/utils/formatters';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
} from '../../../src/theme';

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
          <ArrowLeft size={24} color={Colors.foreground} />
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
            icon={MapPin}
            onPress={() => router.push('/(app)/settings/wards')}
          />
          <NavItem
            label="Notifications"
            icon={Bell}
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
            <ActivityIndicator color={Colors.primary} size="small" />
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
  icon: Icon,
  onPress,
}: {
  label: string;
  icon: LucideIcon;
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
      <Icon size={20} color={Colors.foreground} />
      <Text style={styles.navItemLabel}>{label}</Text>
      <ChevronRight size={16} color={Colors.faintForeground} />
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: Colors.background },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: Spacing.lg,
    paddingVertical: Spacing.md,
    backgroundColor: Colors.card,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
  },
  navTitle: {
    fontSize: FontSize.cardTitle,
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
  },
  navSpacer: { width: 24 },
  content: {
    padding: Spacing.xl,
    gap: Spacing.lg,
    paddingBottom: Spacing['4xl'] - 8,
  },
  card: {
    backgroundColor: Colors.card,
    borderRadius: Radius.lg,
    paddingHorizontal: Spacing.lg,
    borderWidth: 1,
    borderColor: Colors.border,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
    gap: Spacing.lg,
  },
  infoLabel: { fontSize: FontSize.body, color: Colors.mutedForeground },
  infoValue: {
    fontSize: FontSize.body,
    color: Colors.foreground,
    fontFamily: FontFamily.medium,
    flex: 1,
    textAlign: 'right',
  },
  nav: {
    backgroundColor: Colors.card,
    borderRadius: Radius.lg,
    borderWidth: 1,
    borderColor: Colors.border,
    overflow: 'hidden',
  },
  navItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.md,
    paddingVertical: Spacing.md,
    paddingHorizontal: Spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
  },
  navItemLabel: {
    flex: 1,
    fontSize: FontSize.cardTitle,
    color: Colors.foreground,
  },
  signOutBtn: {
    marginTop: Spacing.sm,
    backgroundColor: Colors.card,
    borderWidth: 1.5,
    borderColor: Colors.primary,
    borderRadius: Radius.md,
    paddingVertical: Spacing.md,
    alignItems: 'center',
  },
  signOutBtnBusy: { opacity: 0.7 },
  signOutBtnText: {
    color: Colors.primary,
    fontSize: FontSize.cardTitle,
    fontFamily: FontFamily.semiBold,
  },
  version: {
    fontSize: FontSize.badge,
    color: Colors.faintForeground,
    textAlign: 'center',
  },
});
