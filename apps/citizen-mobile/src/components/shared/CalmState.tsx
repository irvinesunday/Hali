import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { MapPin } from 'lucide-react-native';
import { Colors, FontFamily, FontSize, Spacing, Radius } from '../../theme';

interface CalmStateProps {
  localityName: string;
  lastCheckedText?: string;
}

export function CalmState({ localityName, lastCheckedText }: CalmStateProps) {
  return (
    <View style={styles.container}>
      <View style={styles.iconCircle}>
        <MapPin size={20} color={Colors.emerald} strokeWidth={2} />
      </View>
      <Text style={styles.title}>All clear in {localityName}</Text>
      {lastCheckedText ? (
        <Text style={styles.subtitle}>{lastCheckedText}</Text>
      ) : null}
      <Text style={styles.hint}>Monitoring for new signals…</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    paddingVertical: Spacing['4xl'],
    alignItems: 'center',
    gap: Spacing.sm,
  },
  iconCircle: {
    width: 48,
    height: 48,
    borderRadius: Radius.full,
    backgroundColor: Colors.emeraldSubtle,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: Spacing.xs,
  },
  title: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
  },
  subtitle: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
  },
  hint: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.faintForeground,
    marginTop: Spacing.xs,
  },
});
