// apps/citizen-mobile/app/(auth)/phone.tsx
//
// Phone entry screen — Kenya (+254) prefix.
// POST /v1/auth/otp then navigate to /(auth)/otp with destination.
// States: idle | loading | error.

import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { router } from 'expo-router';
import { requestOtp } from '../../src/api/auth';
import { STRINGS } from '../../src/config/strings';
import {
  normaliseKenyaPhone,
  isValidKenyaPhoneInput,
} from '../../src/utils/phone';

type ScreenState = 'idle' | 'loading' | 'error';

const KENYA_PREFIX = '+254';

export default function PhoneScreen(): React.ReactElement {
  const [phoneInput, setPhoneInput] = useState<string>('');
  const [screenState, setScreenState] = useState<ScreenState>('idle');
  const [errorMessage, setErrorMessage] = useState<string>('');

  const isSubmittable =
    screenState !== 'loading' && isValidKenyaPhoneInput(phoneInput);

  async function handleSubmit(): Promise<void> {
    const destination = normaliseKenyaPhone(phoneInput);
    if (destination === null) return;

    setScreenState('loading');
    setErrorMessage('');

    const result = await requestOtp({
      destination,
      authMethod: 'phone_otp',
    });

    if (result.ok) {
      setScreenState('idle');
      router.push({
        pathname: '/(auth)/otp',
        params: { destination },
      });
    } else {
      setScreenState('error');
      // Auth rate limit comes back as HTTP 429 with { error: "..." } —
      // no code field, so we detect by status instead.
      setErrorMessage(
        result.error.status === 429
          ? STRINGS.AUTH.OTP_RATE_LIMIT
          : result.error.message || STRINGS.AUTH.OTP_REQUEST_FAILED,
      );
    }
  }

  return (
    <KeyboardAvoidingView
      style={styles.flex}
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
    >
      <View style={styles.container}>
        <Text style={styles.title}>{STRINGS.AUTH.PHONE_TITLE}</Text>
        <Text style={styles.subtitle}>{STRINGS.AUTH.PHONE_SUBTITLE}</Text>

        <View style={styles.inputRow}>
          <View
            style={styles.prefixContainer}
            accessible
            accessibilityLabel="Country code Kenya plus two five four"
          >
            <Text style={styles.prefixText}>{KENYA_PREFIX}</Text>
          </View>
          <TextInput
            style={styles.input}
            value={phoneInput}
            onChangeText={(text) => {
              setPhoneInput(text);
              if (screenState === 'error') {
                setScreenState('idle');
                setErrorMessage('');
              }
            }}
            keyboardType="phone-pad"
            placeholder={STRINGS.AUTH.PHONE_PLACEHOLDER}
            placeholderTextColor="#9CA3AF"
            maxLength={12}
            returnKeyType="done"
            onSubmitEditing={isSubmittable ? handleSubmit : undefined}
            autoFocus
            accessible
            accessibilityLabel={STRINGS.AUTH.PHONE_INPUT_LABEL}
            accessibilityHint={STRINGS.AUTH.PHONE_INPUT_HINT}
            editable={screenState !== 'loading'}
          />
        </View>

        {screenState === 'error' && errorMessage !== '' && (
          <Text
            style={styles.errorText}
            accessible
            accessibilityRole="alert"
            accessibilityLiveRegion="polite"
          >
            {errorMessage}
          </Text>
        )}

        <TouchableOpacity
          style={[styles.button, !isSubmittable && styles.buttonDisabled]}
          onPress={handleSubmit}
          disabled={!isSubmittable}
          accessible
          accessibilityRole="button"
          accessibilityLabel={STRINGS.AUTH.PHONE_SUBMIT_LABEL}
          accessibilityState={{
            disabled: !isSubmittable,
            busy: screenState === 'loading',
          }}
        >
          {screenState === 'loading' ? (
            <ActivityIndicator color="#FFFFFF" size="small" />
          ) : (
            <Text style={styles.buttonText}>{STRINGS.AUTH.PHONE_SUBMIT}</Text>
          )}
        </TouchableOpacity>

        <Text style={styles.note}>{STRINGS.AUTH.PHONE_NOTE}</Text>
      </View>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: '#FFFFFF' },
  container: {
    flex: 1,
    paddingHorizontal: 24,
    paddingTop: 64,
    paddingBottom: 32,
  },
  title: {
    fontSize: 28,
    fontWeight: '700',
    color: '#111827',
    marginBottom: 8,
  },
  subtitle: {
    fontSize: 15,
    color: '#6B7280',
    marginBottom: 32,
    lineHeight: 22,
  },
  inputRow: {
    flexDirection: 'row',
    borderWidth: 1.5,
    borderColor: '#D1D5DB',
    borderRadius: 10,
    overflow: 'hidden',
    marginBottom: 8,
  },
  prefixContainer: {
    backgroundColor: '#F3F4F6',
    paddingHorizontal: 14,
    paddingVertical: 14,
    justifyContent: 'center',
    borderRightWidth: 1,
    borderRightColor: '#D1D5DB',
  },
  prefixText: {
    fontSize: 16,
    color: '#374151',
    fontWeight: '600',
  },
  input: {
    flex: 1,
    fontSize: 18,
    paddingHorizontal: 14,
    paddingVertical: 14,
    color: '#111827',
    letterSpacing: 0.5,
  },
  errorText: {
    fontSize: 14,
    color: '#DC2626',
    marginBottom: 12,
    marginTop: 4,
  },
  button: {
    backgroundColor: '#1a3a2f',
    borderRadius: 10,
    paddingVertical: 16,
    alignItems: 'center',
    marginTop: 8,
  },
  buttonDisabled: {
    backgroundColor: '#9CA3AF',
  },
  buttonText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '600',
  },
  note: {
    fontSize: 13,
    color: '#9CA3AF',
    marginTop: 20,
    textAlign: 'center',
    lineHeight: 19,
  },
});
