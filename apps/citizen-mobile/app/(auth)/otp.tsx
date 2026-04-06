// apps/citizen-mobile/app/(auth)/otp.tsx
//
// OTP verification screen — 6-digit code.
// Reads { destination } from route params.
// POST /v1/auth/verify → signIn() → router.replace('/(app)/home').
// Auto-submits as soon as the 6th digit is entered.
// States: idle | loading | error.

import React, { useCallback, useEffect, useRef, useState } from 'react';
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
import { router, useLocalSearchParams } from 'expo-router';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import * as Crypto from 'expo-crypto';
import { verifyOtp } from '../../src/api/auth';
import { useAuth } from '../../src/context/AuthContext';
import { STRINGS } from '../../src/config/strings';

type ScreenState = 'idle' | 'loading' | 'error';

const OTP_LENGTH = 6;

/**
 * Derive a stable device fingerprint hash using expo-crypto.
 * Uses Expo Device / Application identifiers that are available without
 * extra permissions. Returns a SHA-256 hex digest of the joined parts.
 *
 * NOTE: Do NOT use Node's `Buffer` in React Native — it is not available
 * in Hermes without a polyfill. Use expo-crypto instead.
 */
async function getDeviceFingerprintHash(): Promise<string> {
  const parts: string[] = [
    Device.modelName ?? 'unknown',
    Device.osName ?? 'unknown',
    Device.osVersion ?? 'unknown',
    Device.brand ?? 'unknown',
    Application.applicationId ?? 'unknown',
  ];
  const joined = parts.join('|');
  return Crypto.digestStringAsync(
    Crypto.CryptoDigestAlgorithm.SHA256,
    joined,
  );
}

function getPlatform(): string {
  return Platform.OS; // 'ios' | 'android' | 'web'
}

function getAppVersion(): string {
  return Application.nativeApplicationVersion ?? '1.0.0';
}

export default function OtpScreen(): React.ReactElement {
  const { destination } = useLocalSearchParams<{ destination: string }>();
  const { signIn } = useAuth();

  const [otp, setOtp] = useState<string>('');
  const [screenState, setScreenState] = useState<ScreenState>('idle');
  const [errorMessage, setErrorMessage] = useState<string>('');

  const inputRef = useRef<TextInput>(null);

  // Auto-focus on mount
  useEffect(() => {
    const timer = setTimeout(() => {
      inputRef.current?.focus();
    }, 300);
    return () => clearTimeout(timer);
  }, []);

  const handleSubmit = useCallback(async (): Promise<void> => {
    if (otp.length !== OTP_LENGTH) return;
    if (screenState === 'loading') return;
    if (!destination) {
      setScreenState('error');
      setErrorMessage(STRINGS.AUTH.OTP_MISSING_DESTINATION);
      return;
    }

    setScreenState('loading');
    setErrorMessage('');

    let deviceFingerprintHash: string;
    try {
      deviceFingerprintHash = await getDeviceFingerprintHash();
    } catch {
      deviceFingerprintHash = 'unknown-device';
    }

    const result = await verifyOtp({
      destination,
      otp,
      deviceFingerprintHash,
      platform: getPlatform(),
      appVersion: getAppVersion(),
    });

    if (result.ok) {
      await signIn({
        accessToken: result.value.accessToken,
        refreshToken: result.value.refreshToken,
        accountId: result.value.accountId,
      });
      // Root layout will pick up the status change and route into (app)
      router.replace('/(app)/home');
      return;
    }

    setScreenState('error');
    setOtp('');

    if (result.error.status === 401 || result.error.code === 'invalid_otp') {
      setErrorMessage(STRINGS.AUTH.OTP_INVALID);
    } else if (result.error.code === 'otp_expired') {
      setErrorMessage(STRINGS.AUTH.OTP_EXPIRED);
    } else {
      setErrorMessage(result.error.message || STRINGS.AUTH.OTP_VERIFY_FAILED);
    }

    setTimeout(() => inputRef.current?.focus(), 100);
  }, [otp, screenState, destination, signIn]);

  // Auto-submit when 6 digits are entered.
  // Dependency list is intentionally narrow — depending on `otp.length` rather
  // than the full `otp` string avoids recreating the effect on each keystroke.
  // handleSubmit is guarded by the length and screenState checks above.
  const otpLength = otp.length;
  useEffect(() => {
    if (otpLength === OTP_LENGTH && screenState === 'idle') {
      void handleSubmit();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [otpLength, screenState]);

  const maskedDestination =
    destination && destination.length >= 4
      ? `${destination.slice(0, 5)} ••• ${destination.slice(-3)}`
      : destination ?? '';

  return (
    <KeyboardAvoidingView
      style={styles.flex}
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
    >
      <View style={styles.container}>
        <Text style={styles.title}>{STRINGS.AUTH.OTP_TITLE}</Text>
        <Text style={styles.subtitle}>
          {STRINGS.AUTH.OTP_SUBTITLE_PREFIX}
          <Text style={styles.destinationText}>{maskedDestination}</Text>
        </Text>

        {/* Hidden input that captures the keyboard */}
        <TextInput
          ref={inputRef}
          style={styles.hiddenInput}
          value={otp}
          onChangeText={(text) => {
            const clean = text.replace(/\D/g, '').slice(0, OTP_LENGTH);
            setOtp(clean);
            if (screenState === 'error') {
              setScreenState('idle');
              setErrorMessage('');
            }
          }}
          keyboardType="number-pad"
          maxLength={OTP_LENGTH}
          autoComplete="one-time-code"
          textContentType="oneTimeCode"
          importantForAutofill="yes"
          accessible
          accessibilityLabel={STRINGS.AUTH.OTP_INPUT_LABEL}
          editable={screenState !== 'loading'}
        />

        {/* Visible OTP boxes */}
        <View style={styles.otpBoxRow}>
          {Array.from({ length: OTP_LENGTH }).map((_, i) => {
            const char = otp[i] ?? '';
            const isActive = i === otp.length && screenState !== 'loading';
            return (
              <TouchableOpacity
                key={i}
                style={[
                  styles.otpBox,
                  isActive && styles.otpBoxActive,
                  screenState === 'error' && styles.otpBoxError,
                ]}
                onPress={() => inputRef.current?.focus()}
                accessible={false}
              >
                <Text style={styles.otpChar}>
                  {screenState === 'loading' && char !== '' ? '•' : char}
                </Text>
              </TouchableOpacity>
            );
          })}
        </View>

        {screenState === 'loading' && (
          <ActivityIndicator
            style={styles.loadingIndicator}
            color="#1a3a2f"
            size="small"
            accessible
            accessibilityLabel={STRINGS.AUTH.OTP_VERIFYING}
          />
        )}

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

        {/* Manual submit fallback if auto-submit fails */}
        {screenState !== 'loading' && otp.length === OTP_LENGTH && (
          <TouchableOpacity
            style={styles.button}
            onPress={handleSubmit}
            accessible
            accessibilityRole="button"
            accessibilityLabel={STRINGS.AUTH.OTP_SUBMIT_LABEL}
          >
            <Text style={styles.buttonText}>{STRINGS.AUTH.OTP_SUBMIT}</Text>
          </TouchableOpacity>
        )}

        <TouchableOpacity
          style={styles.resendContainer}
          onPress={() => router.back()}
          accessible
          accessibilityRole="button"
          accessibilityLabel={STRINGS.AUTH.OTP_RESEND}
        >
          <Text style={styles.resendText}>{STRINGS.AUTH.OTP_RESEND}</Text>
        </TouchableOpacity>
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
    alignItems: 'center',
  },
  title: {
    fontSize: 28,
    fontWeight: '700',
    color: '#111827',
    marginBottom: 8,
    alignSelf: 'flex-start',
  },
  subtitle: {
    fontSize: 15,
    color: '#6B7280',
    marginBottom: 40,
    lineHeight: 22,
    alignSelf: 'flex-start',
  },
  destinationText: {
    color: '#111827',
    fontWeight: '600',
  },
  hiddenInput: {
    position: 'absolute',
    opacity: 0,
    height: 0,
    width: 0,
  },
  otpBoxRow: {
    flexDirection: 'row',
    gap: 10,
    marginBottom: 24,
  },
  otpBox: {
    width: 48,
    height: 56,
    borderWidth: 1.5,
    borderColor: '#D1D5DB',
    borderRadius: 8,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#F9FAFB',
  },
  otpBoxActive: {
    borderColor: '#1a3a2f',
    backgroundColor: '#F0FDF4',
  },
  otpBoxError: {
    borderColor: '#DC2626',
    backgroundColor: '#FEF2F2',
  },
  otpChar: {
    fontSize: 22,
    fontWeight: '700',
    color: '#111827',
  },
  loadingIndicator: {
    marginVertical: 8,
  },
  errorText: {
    fontSize: 14,
    color: '#DC2626',
    marginBottom: 16,
    textAlign: 'center',
  },
  button: {
    backgroundColor: '#1a3a2f',
    borderRadius: 10,
    paddingVertical: 16,
    paddingHorizontal: 48,
    alignItems: 'center',
    marginBottom: 16,
    width: '100%',
  },
  buttonText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '600',
  },
  resendContainer: {
    marginTop: 8,
    padding: 8,
  },
  resendText: {
    fontSize: 14,
    color: '#1a3a2f',
    fontWeight: '500',
  },
});
