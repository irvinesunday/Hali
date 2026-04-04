import React from 'react';
import { View, Text, StyleSheet } from 'react-native';

interface ParticipationBarProps {
  affectedCount: number;
  observingCount: number;
}

export function ParticipationBar({
  affectedCount,
  observingCount,
}: ParticipationBarProps) {
  return (
    <View style={styles.container}>
      <View style={styles.stat}>
        <Text style={styles.count}>{affectedCount}</Text>
        <Text style={styles.label}>Affected</Text>
      </View>
      <View style={styles.divider} />
      <View style={styles.stat}>
        <Text style={styles.count}>{observingCount}</Text>
        <Text style={styles.label}>Observing</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    backgroundColor: '#f9fafb',
    borderRadius: 10,
    padding: 16,
    gap: 16,
  },
  stat: { flex: 1, alignItems: 'center' },
  count: { fontSize: 22, fontWeight: '700', color: '#111827' },
  label: { fontSize: 13, color: '#6b7280', marginTop: 2 },
  divider: { width: 1, backgroundColor: '#e5e7eb' },
});
