// apps/citizen-mobile/app/(app)/compose/text.tsx
import React, { useEffect, useState } from 'react';
import {
  View, Text, TextInput, ScrollView, TouchableOpacity,
  StyleSheet, KeyboardAvoidingView, Platform,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { X } from 'lucide-react-native';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import * as Crypto from 'expo-crypto';
import { previewSignal } from '../../../src/api/signals';
import { useComposerContext } from '../../../src/context/ComposerContext';
import { Button } from '../../../src/components/common/Button';
import { SIGNAL_TEXT_MAX_LENGTH } from '../../../src/config/constants';
import { ERROR_CODES, isKnownErrorCode } from '../../../src/types/api';
import {
  Colors, FontFamily, FontSize, Spacing, Radius, ScreenPaddingH,
} from '../../../src/theme';

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
          Crypto.CryptoDigestAlgorithm.SHA256, parts,
        );
        setDeviceHash(hash);
      } catch {
        setDeviceHash('unknown-device');
      }
    }
    void initDeviceHash();
  }, [setDeviceHash]);

  const canPreview = screenState !== 'loading' && text.trim().length >= MIN_TEXT_LENGTH;

  async function handlePreview(): Promise<void> {
    if (!canPreview) return;
    setScreenState('loading');
    setErrorMessage('');
    const freeText = text.trim();
    const locationHint = locationInput.trim() || undefined;
    const result = await previewSignal({ freeText, selectedWard: locationHint, countryCode: 'KE' });
    if (result.ok) {
      setFreeText(freeText);
      setLocationHint(locationHint);
      setPreview(result.value);
      setScreenState('idle');
      router.push('/(app)/compose/confirm');
      return;
    }
    setScreenState('error');
    // Branch on the canonical wire code first; status 429 stays as a
    // belt-and-braces fallback for any rate-limited response that does
    // not carry the typed code.
    const isRateLimited =
      (isKnownErrorCode(result.error.code) &&
        result.error.code === ERROR_CODES.RATE_LIMIT_EXCEEDED) ||
      result.error.status === 429;
    if (isRateLimited) {
      setErrorMessage('Too many preview requests. Please wait a moment and try again.');
    } else if (result.error.status === 502) {
      setErrorMessage('The signal analysis service is temporarily unavailable. Please try again shortly.');
    } else {
      setErrorMessage(result.error.message || 'Could not analyse your report. Please try again.');
    }
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity onPress={() => router.back()} hitSlop={12}
          accessibilityRole="button" accessibilityLabel="Close composer">
          <X size={24} color={Colors.foreground} strokeWidth={2} />
        </TouchableOpacity>
        <Text style={styles.stepLabel}>Step 1 of 3</Text>
        <View style={styles.navSpacer} />
      </View>

      <KeyboardAvoidingView style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        keyboardVerticalOffset={80}>
        <ScrollView style={styles.flex} contentContainerStyle={styles.content}
          keyboardShouldPersistTaps="handled">
          <Text style={styles.heading}>What's happening?</Text>
          <Text style={styles.sub}>
            Describe what you're experiencing in your own words.
          </Text>

          <View style={styles.inputWrapper}>
            <TextInput
              style={[styles.textArea, screenState === 'error' && styles.inputError]}
              value={text}
              onChangeText={(v) => {
                setText(v.slice(0, SIGNAL_TEXT_MAX_LENGTH));
                if (screenState === 'error') { setScreenState('idle'); setErrorMessage(''); }
              }}
              placeholder="e.g. There's a large pothole on Ngong Road near Junction Mall…"
              placeholderTextColor={Colors.faintForeground}
              multiline numberOfLines={5} textAlignVertical="top"
              editable={screenState !== 'loading'}
              accessibilityLabel="Signal description"
              accessibilityHint={`Maximum ${SIGNAL_TEXT_MAX_LENGTH} characters`}
            />
            <Text style={styles.counter}>{text.length}/{SIGNAL_TEXT_MAX_LENGTH}</Text>
          </View>

          <Text style={styles.inputLabel}>Location hint (optional)</Text>
          <TextInput
            style={styles.locationInput}
            value={locationInput}
            onChangeText={setLocationInput}
            placeholder="e.g. near Yaya Centre, a road name, or landmark"
            placeholderTextColor={Colors.faintForeground}
            editable={screenState !== 'loading'}
            accessibilityLabel="Location hint"
          />

          {screenState === 'error' && errorMessage !== '' && (
            <Text style={styles.error} accessibilityRole="alert" accessibilityLiveRegion="polite">{errorMessage}</Text>
          )}
          {screenState === 'loading' && (
            <Text style={styles.analysing}>Analysing your report…</Text>
          )}

          <Button label="Preview signal" onPress={handlePreview}
            disabled={!canPreview} loading={screenState === 'loading'}
            accessibilityLabel="Preview signal"
            accessibilityState={{ disabled: !canPreview, busy: screenState === 'loading' }} />
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: Colors.card },
  flex: { flex: 1 },
  navBar: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
    paddingHorizontal: ScreenPaddingH, paddingVertical: Spacing.sm + 2,
  },
  stepLabel: { fontSize: FontSize.bodySmall, fontFamily: FontFamily.medium, color: Colors.mutedForeground },
  navSpacer: { width: 24 },
  content: { paddingHorizontal: ScreenPaddingH, paddingTop: Spacing.lg, paddingBottom: Spacing['4xl'], gap: Spacing.lg },
  heading: { fontSize: FontSize.title, fontFamily: FontFamily.bold, color: Colors.foreground },
  sub: { fontSize: FontSize.body, fontFamily: FontFamily.regular, color: Colors.mutedForeground, lineHeight: FontSize.body * 1.5 },
  inputWrapper: { gap: Spacing.xs },
  textArea: {
    borderWidth: 1.5, borderColor: Colors.border, borderRadius: Radius.md,
    padding: Spacing.lg, fontSize: FontSize.body, fontFamily: FontFamily.regular,
    color: Colors.foreground, backgroundColor: Colors.card, minHeight: 130,
  },
  inputError: { borderColor: Colors.destructive },
  counter: { fontSize: FontSize.micro, fontFamily: FontFamily.regular, color: Colors.faintForeground, textAlign: 'right' },
  inputLabel: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.medium,
    color: Colors.mutedForeground,
    marginBottom: Spacing.xs,
  },
  locationInput: {
    borderWidth: 1.5, borderColor: Colors.border, borderRadius: Radius.md,
    paddingHorizontal: Spacing.lg, paddingVertical: Spacing.md,
    fontSize: FontSize.body, fontFamily: FontFamily.regular,
    color: Colors.foreground, backgroundColor: Colors.card,
  },
  error: { fontSize: FontSize.bodySmall, fontFamily: FontFamily.regular, color: Colors.destructive },
  analysing: { fontSize: FontSize.bodySmall, fontFamily: FontFamily.regular, color: Colors.mutedForeground, fontStyle: 'italic', textAlign: 'center' },
});
