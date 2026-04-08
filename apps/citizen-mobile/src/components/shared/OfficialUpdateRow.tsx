// apps/citizen-mobile/src/components/shared/OfficialUpdateRow.tsx
//
// Deliberately subdued — left border only, muted background.
// Must never appear more prominent than ClusterCard.
// Doctrine: official updates and citizen signals are dual-visible;
// neither overrides the other.

import React from 'react';
import { TouchableOpacity, View, Text, StyleSheet } from 'react-native';
import { Building2 } from 'lucide-react-native';
import { Colors, FontFamily, FontSize, Spacing, Radius } from '../../theme';

interface OfficialUpdateRowProps {
  institutionName: string;
  message: string;
  onPress?: () => void;
}

export function OfficialUpdateRow({
  institutionName,
  message,
  onPress,
}: OfficialUpdateRowProps) {
  return (
    <TouchableOpacity
      style={styles.row}
      onPress={onPress}
      activeOpacity={0.7}
      disabled={!onPress}
      accessibilityRole="button"
      accessibilityLabel={`${institutionName}: ${message}`}
    >
      <Building2 size={14} color={Colors.mutedForeground} strokeWidth={2} />
      <View style={styles.content}>
        <Text style={styles.text} numberOfLines={2}>
          <Text style={styles.institution}>{institutionName}</Text>
          <Text style={styles.dot}> · </Text>
          {message}
        </Text>
      </View>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: Spacing.sm,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm + 2,
    backgroundColor: Colors.muted,
    borderLeftWidth: 2,
    borderLeftColor: Colors.border,
    borderRadius: Radius.xs,
  },
  content: { flex: 1 },
  text: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
    lineHeight: FontSize.bodySmall * 1.4,
  },
  institution: {
    fontFamily: FontFamily.medium,
    color: Colors.foreground,
  },
  dot: { color: Colors.faintForeground },
});
