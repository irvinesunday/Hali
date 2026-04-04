// Account Settings — user info + logout
import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  Alert,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useQuery } from '@tanstack/react-query';
import { getMe } from '../../../src/api/users';
import { useAuthContext } from '../../../src/context/AuthContext';
import { Button } from '../../../src/components/common/Button';
import { Loading } from '../../../src/components/common/Loading';
import { formatRelativeTime } from '../../../src/utils/formatters';

export default function AccountSettingsScreen() {
  const router = useRouter();
  const { signOut } = useAuthContext();
  const [signingOut, setSigningOut] = useState(false);

  const { data: me, isLoading } = useQuery({
    queryKey: ['users', 'me'],
    queryFn: getMe,
  });

  function handleLogout() {
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
          } finally {
            setSigningOut(false);
          }
        },
      },
    ]);
  }

  if (isLoading) return <Loading />;

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity onPress={() => router.back()} hitSlop={12}>
          <Ionicons name="arrow-back" size={24} color="#111827" />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Account</Text>
        <View style={{ width: 24 }} />
      </View>

      <ScrollView contentContainerStyle={styles.content}>
        {me && (
          <View style={styles.card}>
            <InfoRow label="Phone" value={me.phoneE164 ?? '—'} />
            <InfoRow
              label="Email"
              value={me.email ?? '—'}
            />
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

        <Button
          label="Sign out"
          variant="secondary"
          onPress={handleLogout}
          loading={signingOut}
          style={styles.signOutBtn}
        />

        <Text style={styles.version}>Hali v1.0.0</Text>
      </ScrollView>
    </SafeAreaView>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.infoRow}>
      <Text style={styles.infoLabel}>{label}</Text>
      <Text style={styles.infoValue}>{value}</Text>
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
}) {
  return (
    <TouchableOpacity style={styles.navItem} onPress={onPress} activeOpacity={0.7}>
      <Ionicons name={icon} size={20} color="#374151" />
      <Text style={styles.navItemLabel}>{label}</Text>
      <Ionicons name="chevron-forward" size={16} color="#9ca3af" />
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#f9fafb' },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e5e7eb',
  },
  navTitle: { fontSize: 17, fontWeight: '600', color: '#111827' },
  content: { padding: 20, gap: 16, paddingBottom: 40 },
  card: {
    backgroundColor: '#fff',
    borderRadius: 12,
    paddingHorizontal: 16,
    borderWidth: 1,
    borderColor: '#e5e7eb',
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 14,
    borderBottomWidth: 1,
    borderBottomColor: '#f3f4f6',
    gap: 16,
  },
  infoLabel: { fontSize: 14, color: '#6b7280' },
  infoValue: { fontSize: 14, color: '#111827', fontWeight: '500', flex: 1, textAlign: 'right' },
  nav: {
    backgroundColor: '#fff',
    borderRadius: 12,
    borderWidth: 1,
    borderColor: '#e5e7eb',
    overflow: 'hidden',
  },
  navItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    paddingVertical: 14,
    paddingHorizontal: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#f3f4f6',
  },
  navItemLabel: { flex: 1, fontSize: 15, color: '#111827' },
  signOutBtn: { marginTop: 8 },
  version: { fontSize: 12, color: '#9ca3af', textAlign: 'center' },
});
