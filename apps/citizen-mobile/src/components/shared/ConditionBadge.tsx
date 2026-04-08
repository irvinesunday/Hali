import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
  getConditionBadgePalette,
} from '../../theme';

interface ConditionBadgeProps {
  label: string;
}

export function ConditionBadge({ label }: ConditionBadgeProps) {
  const palette = getConditionBadgePalette(label);
  const colors = Colors.conditionBadge[palette];

  return (
    <View
      style={[
        styles.badge,
        { backgroundColor: colors.bg, borderColor: colors.border },
      ]}
    >
      <Text style={[styles.text, { color: colors.text }]}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  badge: {
    alignSelf: 'flex-start',
    borderRadius: Radius.full,
    borderWidth: 1,
    paddingHorizontal: Spacing.sm,
    paddingVertical: 3,
  },
  text: {
    fontSize: FontSize.badge,
    fontFamily: FontFamily.medium,
  },
});
