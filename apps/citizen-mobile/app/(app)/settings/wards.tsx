// Ward Following Settings — Flow F
import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  TextInput,
  Alert,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getFollowedLocalities,
  setFollowedLocalities,
} from '../../../src/api/localities';
import { useLocalityContext } from '../../../src/context/LocalityContext';
import { Button } from '../../../src/components/common/Button';
import { Loading } from '../../../src/components/common/Loading';
import { MAX_FOLLOWED_WARDS } from '../../../src/config/constants';

export default function WardsSettingsScreen() {
  const router = useRouter();
  const qc = useQueryClient();
  const { setFollowedLocalityIds, setActiveLocalityId } = useLocalityContext();

  const [newWardId, setNewWardId] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['localities', 'followed'],
    queryFn: getFollowedLocalities,
  });

  const currentIds = data?.localityIds ?? [];

  const updateMutation = useMutation({
    mutationFn: (ids: string[]) => setFollowedLocalities({ localityIds: ids }),
    onSuccess: (_, ids) => {
      setFollowedLocalityIds(ids);
      if (ids.length > 0 && !ids.includes('')) {
        setActiveLocalityId(ids[0]);
      }
      qc.invalidateQueries({ queryKey: ['localities', 'followed'] });
    },
    onError: (err: unknown) => {
      const message =
        err instanceof Error ? err.message : 'Could not update followed wards.';
      Alert.alert('Error', message);
    },
  });

  function handleAdd() {
    const trimmed = newWardId.trim();
    if (!trimmed) return;
    if (currentIds.includes(trimmed)) {
      Alert.alert('Already following', 'You are already following this ward.');
      return;
    }
    if (currentIds.length >= MAX_FOLLOWED_WARDS) {
      Alert.alert(
        'Limit reached',
        `You can follow at most ${MAX_FOLLOWED_WARDS} wards.`,
      );
      return;
    }
    const updated = [...currentIds, trimmed];
    updateMutation.mutate(updated);
    setNewWardId('');
  }

  function handleRemove(id: string) {
    const updated = currentIds.filter((w) => w !== id);
    updateMutation.mutate(updated);
  }

  if (isLoading) return <Loading />;

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity onPress={() => router.back()} hitSlop={12}>
          <Ionicons name="arrow-back" size={24} color="#111827" />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Ward following</Text>
        <View style={{ width: 24 }} />
      </View>

      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.badge}>
          {currentIds.length} / {MAX_FOLLOWED_WARDS} wards followed
        </Text>

        {currentIds.length === 0 && (
          <Text style={styles.empty}>
            You're not following any wards yet. Add a ward ID below.
          </Text>
        )}

        {currentIds.map((id) => (
          <View key={id} style={styles.wardRow}>
            <Text style={styles.wardId} numberOfLines={1}>
              {id}
            </Text>
            <TouchableOpacity
              onPress={() => handleRemove(id)}
              hitSlop={8}
              disabled={updateMutation.isPending}
            >
              <Ionicons name="close-circle" size={22} color="#dc2626" />
            </TouchableOpacity>
          </View>
        ))}

        <View style={styles.addRow}>
          <TextInput
            style={styles.addInput}
            value={newWardId}
            onChangeText={setNewWardId}
            placeholder="Ward / locality ID (UUID)"
            placeholderTextColor="#9ca3af"
            autoCapitalize="none"
            autoCorrect={false}
          />
          <Button
            label="Add"
            onPress={handleAdd}
            loading={updateMutation.isPending}
            disabled={!newWardId.trim() || currentIds.length >= MAX_FOLLOWED_WARDS}
            style={styles.addBtn}
          />
        </View>

        <Text style={styles.hint}>
          Enter the locality ID (UUID) of the ward you want to follow. Maximum{' '}
          {MAX_FOLLOWED_WARDS} wards. The first ward added becomes your active
          home feed ward.
        </Text>
      </ScrollView>
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
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#e5e7eb',
  },
  navTitle: { fontSize: 17, fontWeight: '600', color: '#111827' },
  content: { padding: 20, gap: 14 },
  badge: {
    fontSize: 13,
    fontWeight: '600',
    color: '#1a3a2f',
    backgroundColor: '#f0fdf4',
    borderRadius: 20,
    paddingHorizontal: 12,
    paddingVertical: 4,
    alignSelf: 'flex-start',
  },
  empty: { fontSize: 14, color: '#6b7280' },
  wardRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#f9fafb',
    borderRadius: 10,
    padding: 14,
    gap: 10,
  },
  wardId: { flex: 1, fontSize: 14, color: '#111827', fontFamily: 'monospace' },
  addRow: { flexDirection: 'row', gap: 10, alignItems: 'flex-start' },
  addInput: {
    flex: 1,
    borderWidth: 1.5,
    borderColor: '#d1d5db',
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 12,
    fontSize: 14,
    color: '#111827',
    backgroundColor: '#fff',
  },
  addBtn: { paddingHorizontal: 16 },
  hint: { fontSize: 13, color: '#9ca3af', lineHeight: 18 },
});
