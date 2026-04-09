import React from 'react';
import {
  TextInput as RNTextInput,
  Text,
  View,
  StyleSheet,
  type TextInputProps,
} from 'react-native';
import { Colors } from '../../theme/colors';

interface HaliTextInputProps extends TextInputProps {
  label?: string;
  error?: string;
  hint?: string;
}

export function TextInput({
  label,
  error,
  hint,
  style,
  ...rest
}: HaliTextInputProps) {
  return (
    <View style={styles.wrapper}>
      {label ? <Text style={styles.label}>{label}</Text> : null}
      <RNTextInput
        style={[styles.input, error ? styles.inputError : null, style]}
        placeholderTextColor={Colors.faintForeground}
        {...rest}
      />
      {error ? <Text style={styles.error}>{error}</Text> : null}
      {!error && hint ? <Text style={styles.hint}>{hint}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: { gap: 4 },
  label: { fontSize: 14, fontWeight: '500', color: Colors.foreground },
  input: {
    borderWidth: 1.5,
    borderColor: Colors.border,
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 16,
    color: Colors.foreground,
    backgroundColor: Colors.card,
  },
  inputError: { borderColor: Colors.destructive },
  error: { fontSize: 13, color: Colors.destructive },
  hint: { fontSize: 13, color: Colors.mutedForeground },
});
