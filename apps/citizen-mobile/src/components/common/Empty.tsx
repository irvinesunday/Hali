import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Colors } from '../../theme/colors';

interface EmptyProps {
  message: string;
  subMessage?: string;
}

export function Empty({ message, subMessage }: EmptyProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.message}>{message}</Text>
      {subMessage ? (
        <Text style={styles.subMessage}>{subMessage}</Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    paddingVertical: 24,
    paddingHorizontal: 16,
    alignItems: 'center',
  },
  message: { fontSize: 15, color: Colors.mutedForeground, textAlign: 'center' },
  subMessage: {
    fontSize: 13,
    color: Colors.faintForeground,
    textAlign: 'center',
    marginTop: 4,
  },
});
