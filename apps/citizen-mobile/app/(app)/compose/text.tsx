// apps/citizen-mobile/app/(app)/compose/text.tsx
//
// Signal composer — Step 1: free-text input.
// Calls POST /v1/signals/preview and stores the result in ComposerContext,
// then navigates to Step 2.
//
// Input: user's free-text description (max 500 chars) + optional location
// hint (feeds the NLP as `selectedWard`).
//
// Contract references:
//   SignalPreviewRequest — src/types/api.ts
//   SIGNAL_TEXT_MAX_LENGTH — src/config/constants.ts (500)

import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  TextInput,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import * as Crypto from 'expo-crypto';
import { previewSignal } from '../../../src/api/signals';
import { useComposerContext } from '../../../src/context/ComposerContext';
import { SIGNAL_TEXT_MAX_LENGTH } from '../../../src/config/constants';

type ScreenState = 'idle' | 'loading' | 'error';

const MIN_TEXT_LENGTH = 10;

export default function ComposerTextScreen(): React.ReactElement {
  const router = useRouter();
  const { setFreeText, setLocationHint, setPreview, setDeviceHash } =
    useComposerContext();

  const [text, setText] = useState('');
  const [locationInput, setLocationInput] = useState('');
  const [screenState, setScreenState] = useState<ScreenState>('idle');
  const [errorMessage, setErrorMessage] = useState('');

  // Derive a stable device fingerprint once on mount — same logic as otp.tsx
  // so a device has a consistent identity across the composer and auth flows.
  useEffect(() => {
    async function initDeviceHash(): Promise<void> {
      try {
        const parts = [
          Device.modelName ?? 'unknown',
          Device.osName ?? 'unknown',
          Device.osVersion ?? 'unknown',
          Device.brand ?? 'unknown',
          Application.applicationId ?? 'unknown',
        ].join('|');
        const hash = await Crypto.digestStringAsync(
          Crypto.CryptoDigestAlgorithm.SHA256,
          parts,
        );
        setDeviceHash(hash);
      } catch {
        setDeviceHash('unknown-device');
      }
    }
    void initDeviceHash();
  }, [setDeviceHash]);

  const trimmedLength = text.trim().length;
  const canPreview =
    screenState !== 'loading' && trimmedLength >= MIN_TEXT_LENGTH;

  async function handlePreview(): Promise<void> {
    if (!canPreview) return;

    setScreenState('loading');
    setErrorMessage('');

    const freeText = text.trim();
    const locationHint = locationInput.trim() || undefined;

    const result = await previewSignal({
      freeText,
      selectedWard: locationHint,
      countryCode: 'KE',
    });

    if (result.ok) {
      // Persist composer draft in context so Step 2 / Step 3 can read it.
      setFreeText(freeText);
      setLocationHint(locationHint);
      setPreview(result.value);
      setScreenState('idle');
      router.push('/(app)/compose/confirm');
      return;
    }

    setScreenState('error');
    // Error-body shape was verified in the auth session: the backend
    // returns { error: "..." } or { code: "rate_limited", message: "..." }.
    // buildApiError in client.ts normalises both into ApiError.message.
    if (result.error.status === 429 || result.error.code === 'rate_limited') {
      setErrorMessage(
        'Too many preview requests. Please wait a moment and try again.',
      );
    } else if (result.error.status === 502) {
      setErrorMessage(
        'The signal analysis service is temporarily unavailable. Please try again shortly.',
      );
    } else {
      setErrorMessage(
        result.error.message ||
          'Could not analyse your report. Please try again.',
      );
    }
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity
          onPress={() => router.back()}
          hitSlop={12}
          accessible
          accessibilityRole="button"
          accessibilityLabel="Close composer"
        >
          <Ionicons name="close" size={24} color="#111827" />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Step 1 of 3</Text>
        <View style={styles.navSpacer} />
      </View>

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
          <Text style={styles.heading}>What&apos;s happening?</Text>
          <Text style={styles.sub}>
            Describe what you&apos;re experiencing in your own words.
          </Text>

          <View style={styles.inputWrapper}>
            <TextInput
              style={styles.textArea}
              value={text}
              onChangeText={(v) => {
                setText(v.slice(0, SIGNAL_TEXT_MAX_LENGTH));
                if (screenState === 'error') {
                  setScreenState('idle');
                  setErrorMessage('');
                }
              }}
              placeholder="e.g. There's a large pothole on Ngong Road near Junction Mall…"
              placeholderTextColor="#9CA3AF"
              multiline
              numberOfLines={5}
              textAlignVertical="top"
              editable={screenState !== 'loading'}
              accessible
              accessibilityLabel="Signal description"
              accessibilityHint={`Maximum ${SIGNAL_TEXT_MAX_LENGTH} characters`}
            />
            <Text style={styles.counter}>
              {text.length}/{SIGNAL_TEXT_MAX_LENGTH}
            </Text>
          </View>

          <TextInput
            style={styles.locationInput}
            value={locationInput}
            onChangeText={setLocationInput}
            placeholder="Location hint (optional) — e.g. near Yaya Centre"
            placeholderTextColor="#9CA3AF"
            editable={screenState !== 'loading'}
            accessible
            accessibilityLabel="Location hint"
          />

          {screenState === 'error' && errorMessage !== '' && (
            <Text
              style={styles.error}
              accessible
              accessibilityRole="alert"
              accessibilityLiveRegion="polite"
            >
              {errorMessage}
            </Text>
          )}

          {screenState === 'loading' && (
            <Text style={styles.analysing}>Analysing your report…</Text>
          )}

          <TouchableOpacity
            style={[styles.button, !canPreview && styles.buttonDisabled]}
            onPress={handlePreview}
            disabled={!canPreview}
            accessible
            accessibilityRole="button"
            accessibilityLabel="Preview signal"
            accessibilityState={{
              disabled: !canPreview,
              busy: screenState === 'loading',
            }}
          >
            {screenState === 'loading' ? (
              <ActivityIndicator color="#FFFFFF" size="small" />
            ) : (
              <Text style={styles.buttonText}>Preview signal</Text>
            )}
          </TouchableOpacity>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#FFFFFF' },
  flex: { flex: 1 },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  navTitle: { fontSize: 14, color: '#6B7280', fontWeight: '500' },
  navSpacer: { width: 24 },
  content: { padding: 20, gap: 16 },
  heading: { fontSize: 22, fontWeight: '700', color: '#111827' },
  sub: { fontSize: 15, color: '#6B7280', lineHeight: 22 },
  inputWrapper: { gap: 4 },
  textArea: {
    borderWidth: 1.5,
    borderColor: '#D1D5DB',
    borderRadius: 10,
    padding: 14,
    fontSize: 16,
    color: '#111827',
    backgroundColor: '#FFFFFF',
    minHeight: 130,
  },
  counter: { fontSize: 12, color: '#9CA3AF', textAlign: 'right' },
  locationInput: {
    borderWidth: 1.5,
    borderColor: '#D1D5DB',
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 15,
    color: '#111827',
    backgroundColor: '#FFFFFF',
  },
  error: { fontSize: 14, color: '#DC2626' },
  analysing: {
    fontSize: 14,
    color: '#6B7280',
    fontStyle: 'italic',
    textAlign: 'center',
  },
  button: {
    backgroundColor: '#1a3a2f',
    borderRadius: 10,
    paddingVertical: 16,
    alignItems: 'center',
    marginTop: 8,
  },
  buttonDisabled: { backgroundColor: '#9CA3AF' },
  buttonText: { color: '#FFFFFF', fontSize: 16, fontWeight: '600' },
});
