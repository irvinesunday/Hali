import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  ScrollView,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { Button } from '../common/Button';
import { SIGNAL_TEXT_MAX_LENGTH } from '../../config/constants';

interface ComposerStep1Props {
  onPreview: (freeText: string, locationHint?: string) => void;
  loading: boolean;
  error?: string | null;
}

export function ComposerStep1({ onPreview, loading, error }: ComposerStep1Props) {
  const [text, setText] = useState('');
  const [locationHint, setLocationHint] = useState('');

  const canPreview = text.trim().length >= 10 && !loading;

  return (
    <KeyboardAvoidingView
      style={styles.flex}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={80}
    >
      <ScrollView
        style={styles.flex}
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled"
      >
        <Text style={styles.heading}>What's happening?</Text>
        <Text style={styles.sub}>
          Describe what you're experiencing in your own words.
        </Text>

        <View style={styles.inputWrapper}>
          <TextInput
            style={styles.textArea}
            value={text}
            onChangeText={(v) =>
              setText(v.slice(0, SIGNAL_TEXT_MAX_LENGTH))
            }
            placeholder="e.g. There's a large pothole on Ngong Road near Junction Mall…"
            placeholderTextColor="#9ca3af"
            multiline
            numberOfLines={5}
            textAlignVertical="top"
          />
          <Text style={styles.counter}>
            {text.length}/{SIGNAL_TEXT_MAX_LENGTH}
          </Text>
        </View>

        <TextInput
          style={styles.locationInput}
          value={locationHint}
          onChangeText={setLocationHint}
          placeholder="Location hint (optional) — e.g. near Yaya Centre"
          placeholderTextColor="#9ca3af"
        />

        {error ? <Text style={styles.error}>{error}</Text> : null}

        {loading ? (
          <Text style={styles.analysing}>Analysing your report…</Text>
        ) : null}

        <Button
          label="Preview signal"
          onPress={() => onPreview(text.trim(), locationHint.trim() || undefined)}
          loading={loading}
          disabled={!canPreview}
          style={styles.cta}
        />
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 20, gap: 16 },
  heading: { fontSize: 22, fontWeight: '700', color: '#111827' },
  sub: { fontSize: 15, color: '#6b7280', lineHeight: 22 },
  inputWrapper: { gap: 4 },
  textArea: {
    borderWidth: 1.5,
    borderColor: '#d1d5db',
    borderRadius: 10,
    padding: 14,
    fontSize: 16,
    color: '#111827',
    backgroundColor: '#fff',
    minHeight: 130,
  },
  counter: { fontSize: 12, color: '#9ca3af', textAlign: 'right' },
  locationInput: {
    borderWidth: 1.5,
    borderColor: '#d1d5db',
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 15,
    color: '#111827',
    backgroundColor: '#fff',
  },
  error: { fontSize: 14, color: '#dc2626' },
  analysing: {
    fontSize: 14,
    color: '#6b7280',
    fontStyle: 'italic',
    textAlign: 'center',
  },
  cta: { marginTop: 8 },
});
