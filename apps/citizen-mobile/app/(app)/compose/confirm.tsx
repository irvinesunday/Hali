// Signal Composer Step 2 — Confirm extraction
import React from 'react';
import { View, TouchableOpacity, StyleSheet, Text } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { ComposerStep2 } from '../../../src/components/signals/ComposerStep2';
import { useComposerContext } from '../../../src/context/ComposerContext';
import { Loading } from '../../../src/components/common/Loading';
import type { SignalPreviewResponse } from '../../../src/types/api';

export default function ComposerConfirmScreen() {
  const router = useRouter();
  const { preview, setPreview } = useComposerContext();

  if (!preview) {
    // Guard: should not arrive here without a preview
    return <Loading message="Loading preview…" />;
  }

  function handleConfirm(updated: SignalPreviewResponse) {
    setPreview(updated);
    router.push('/(app)/compose/submit');
  }

  function handleEdit() {
    // Go back to step 1 to let the user rewrite
    router.back();
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity onPress={() => router.back()} hitSlop={12}>
          <Ionicons name="arrow-back" size={24} color="#111827" />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Step 2 of 3</Text>
        <View style={{ width: 24 }} />
      </View>
      <ComposerStep2
        preview={preview}
        onConfirm={handleConfirm}
        onEdit={handleEdit}
      />
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
    paddingVertical: 10,
  },
  navTitle: { fontSize: 14, color: '#6b7280' },
});
