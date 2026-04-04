import React from 'react';
import {
  TextInput as RNTextInput,
  Text,
  View,
  StyleSheet,
  type TextInputProps,
} from 'react-native';

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
        placeholderTextColor="#9ca3af"
        {...rest}
      />
      {error ? <Text style={styles.error}>{error}</Text> : null}
      {!error && hint ? <Text style={styles.hint}>{hint}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: { gap: 4 },
  label: { fontSize: 14, fontWeight: '500', color: '#374151' },
  input: {
    borderWidth: 1.5,
    borderColor: '#d1d5db',
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 16,
    color: '#111827',
    backgroundColor: '#fff',
  },
  inputError: { borderColor: '#dc2626' },
  error: { fontSize: 13, color: '#dc2626' },
  hint: { fontSize: 13, color: '#6b7280' },
});
