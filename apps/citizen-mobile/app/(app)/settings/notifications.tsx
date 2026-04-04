// Notification Settings — push token registration + preferences
import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  Switch,
  Alert,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import * as Notifications from 'expo-notifications';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import * as Crypto from 'expo-crypto';
import { useQuery, useMutation } from '@tanstack/react-query';
import { getMe, updateNotificationSettings } from '../../../src/api/users';
import { registerPushToken } from '../../../src/api/devices';
import { Loading } from '../../../src/components/common/Loading';
import { Button } from '../../../src/components/common/Button';

export default function NotificationSettingsScreen() {
  const router = useRouter();
  const [pushRegistered, setPushRegistered] = useState(false);
  const [registering, setRegistering] = useState(false);

  const { data: me, isLoading } = useQuery({
    queryKey: ['users', 'me'],
    queryFn: getMe,
  });

  const [settings, setSettings] = React.useState({
    clusterActivated: true,
    restorationPrompt: true,
    clusterResolved: true,
  });

  useEffect(() => {
    if (me?.notificationSettings) {
      setSettings(me.notificationSettings);
    }
  }, [me]);

  const updateMutation = useMutation({
    mutationFn: updateNotificationSettings,
    onError: () => {
      Alert.alert('Error', 'Could not update notification settings.');
    },
  });

  function handleToggle(key: keyof typeof settings) {
    const updated = { ...settings, [key]: !settings[key] };
    setSettings(updated);
    updateMutation.mutate(updated);
  }

  async function handleRegisterPush() {
    setRegistering(true);
    try {
      const { status } = await Notifications.requestPermissionsAsync();
      if (status !== 'granted') {
        Alert.alert(
          'Permission denied',
          'Enable notifications in your device settings to receive push alerts.',
        );
        return;
      }
      const token = await Notifications.getExpoPushTokenAsync();
      const raw = `${Device.osName}-${Device.osVersion}-${Device.modelName}-${Application.applicationId}`;
      const deviceHash = await Crypto.digestStringAsync(
        Crypto.CryptoDigestAlgorithm.SHA256,
        raw,
      );
      await registerPushToken({ expoPushToken: token.data, deviceHash });
      setPushRegistered(true);
    } catch {
      Alert.alert('Error', 'Could not register for push notifications.');
    } finally {
      setRegistering(false);
    }
  }

  if (isLoading) return <Loading />;

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity onPress={() => router.back()} hitSlop={12}>
          <Ionicons name="arrow-back" size={24} color="#111827" />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Notifications</Text>
        <View style={{ width: 24 }} />
      </View>

      <ScrollView contentContainerStyle={styles.content}>
        {/* Push registration */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Push notifications</Text>
          {pushRegistered ? (
            <Text style={styles.registered}>
              Push notifications registered.
            </Text>
          ) : (
            <>
              <Text style={styles.sectionSub}>
                Enable push notifications to get alerted when clusters become
                active, or when service may be restored.
              </Text>
              <Button
                label="Enable push notifications"
                onPress={handleRegisterPush}
                loading={registering}
              />
            </>
          )}
        </View>

        {/* Preferences */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Alert preferences</Text>

          <ToggleRow
            label="Cluster activated"
            description="When a signal near you becomes active"
            value={settings.clusterActivated}
            onToggle={() => handleToggle('clusterActivated')}
          />
          <ToggleRow
            label="Restoration prompt"
            description="When service may have been restored"
            value={settings.restorationPrompt}
            onToggle={() => handleToggle('restorationPrompt')}
          />
          <ToggleRow
            label="Cluster resolved"
            description="When a cluster you participated in is resolved"
            value={settings.clusterResolved}
            onToggle={() => handleToggle('clusterResolved')}
          />
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
}: {
  label: string;
  description: string;
  value: boolean;
  onToggle: () => void;
}) {
  return (
    <View style={styles.toggleRow}>
      <View style={styles.toggleText}>
        <Text style={styles.toggleLabel}>{label}</Text>
        <Text style={styles.toggleDesc}>{description}</Text>
      </View>
      <Switch
        value={value}
        onValueChange={onToggle}
        trackColor={{ false: '#d1d5db', true: '#1a3a2f' }}
        thumbColor="#fff"
      />
    </View>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#fff' },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#e5e7eb',
  },
  navTitle: { fontSize: 17, fontWeight: '600', color: '#111827' },
  content: { padding: 20, gap: 24 },
  section: { gap: 12 },
  sectionTitle: { fontSize: 17, fontWeight: '700', color: '#111827' },
  sectionSub: { fontSize: 14, color: '#6b7280', lineHeight: 20 },
  registered: { fontSize: 14, color: '#166534' },
  toggleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#f3f4f6',
    gap: 12,
  },
  toggleText: { flex: 1, gap: 2 },
  toggleLabel: { fontSize: 15, color: '#111827', fontWeight: '500' },
  toggleDesc: { fontSize: 13, color: '#6b7280' },
});
