import React from 'react';
import { Text, StyleSheet } from 'react-native';
import { Colors, FontFamily, FontSize, LetterSpacing } from '../../theme';

interface SectionHeaderProps {
  label: string;
}

export function SectionHeader({ label }: SectionHeaderProps) {
  return (
    <Text style={styles.label} accessibilityRole="header">
      {label.toUpperCase()}
    </Text>
  );
}

const styles = StyleSheet.create({
  label: {
    fontSize: FontSize.sectionHeader,
    fontFamily: FontFamily.medium,
    color: Colors.mutedForeground,
    letterSpacing: LetterSpacing.wide,
  },
});
