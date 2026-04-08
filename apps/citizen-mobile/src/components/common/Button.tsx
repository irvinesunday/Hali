import React from 'react';
import {
  TouchableOpacity,
  Text,
  ActivityIndicator,
  StyleSheet,
  type TouchableOpacityProps,
} from 'react-native';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
} from '../../theme';

interface ButtonProps extends TouchableOpacityProps {
  label: string;
  variant?: 'primary' | 'secondary' | 'ghost' | 'destructive';
  size?: 'md' | 'sm';
  loading?: boolean;
}

export function Button({
  label,
  variant = 'primary',
  size = 'md',
  loading = false,
  disabled,
  style,
  ...rest
}: ButtonProps) {
  const isDisabled = disabled || loading;

  return (
    <TouchableOpacity
      style={[
        styles.base,
        size === 'sm' && styles.sm,
        variant === 'primary' && styles.primary,
        variant === 'secondary' && styles.secondary,
        variant === 'ghost' && styles.ghost,
        variant === 'destructive' && styles.destructive,
        isDisabled && styles.disabled,
        style,
      ]}
      disabled={isDisabled}
      activeOpacity={0.75}
      {...rest}
    >
      {loading ? (
        <ActivityIndicator
          color={
            variant === 'primary' || variant === 'destructive'
              ? Colors.primaryForeground
              : Colors.primary
          }
          size="small"
        />
      ) : (
        <Text
          style={[
            styles.label,
            size === 'sm' && styles.labelSm,
            variant === 'primary' && styles.labelPrimary,
            variant === 'secondary' && styles.labelSecondary,
            variant === 'ghost' && styles.labelGhost,
            variant === 'destructive' && styles.labelDestructive,
          ]}
        >
          {label}
        </Text>
      )}
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  base: {
    borderRadius: Radius.md,
    paddingVertical: Spacing.md,
    paddingHorizontal: Spacing.xl,
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 48,
  },
  sm: {
    paddingVertical: Spacing.sm,
    paddingHorizontal: Spacing.lg,
    minHeight: 36,
  },
  primary:     { backgroundColor: Colors.primary },
  secondary:   { backgroundColor: 'transparent', borderWidth: 1.5, borderColor: Colors.primary },
  ghost:       { backgroundColor: 'transparent' },
  destructive: { backgroundColor: Colors.destructive },
  disabled:    { opacity: 0.45 },
  label: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.semiBold,
    letterSpacing: 0.1,
  },
  labelSm:          { fontSize: FontSize.badge },
  labelPrimary:     { color: Colors.primaryForeground },
  labelSecondary:   { color: Colors.primary },
  labelGhost:       { color: Colors.primary },
  labelDestructive: { color: Colors.primaryForeground },
});
