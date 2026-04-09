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
import { SafeAreaView } from 'react-native-safe-area-context';
import { ArrowLeft } from 'lucide-react-native';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import * as Crypto from 'expo-crypto';
import { verifyOtp } from '../../src/api/auth';
import { useAuth } from '../../src/context/AuthContext';
import { Button } from '../../src/components/common/Button';
import { STRINGS } from '../../src/config/strings';
import { decodeAccountIdFromJwt } from '../../src/utils/jwt';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
  ScreenPaddingH,
} from '../../src/theme';

type ScreenState = 'idle' | 'loading' | 'error';
const OTP_LENGTH = 6;

async function getDeviceFingerprintHash(): Promise<string> {
  const parts = [
    Device.modelName ?? 'unknown',
    Device.osName ?? 'unknown',
    Device.osVersion ?? 'unknown',
    Device.brand ?? 'unknown',
    Application.applicationId ?? 'unknown',
  ];
  return Crypto.digestStringAsync(
    Crypto.CryptoDigestAlgorithm.SHA256,
    parts.join('|'),
  );
}

export default function OtpScreen(): React.ReactElement {
  const { destination } = useLocalSearchParams<{ destination: string }>();
  const { signIn } = useAuth();

  const [otp, setOtp] = useState('');
  const [screenState, setScreenState] = useState<ScreenState>('idle');
  const [errorMessage, setErrorMessage] = useState('');

  const inputRef = useRef<TextInput>(null);

  useEffect(() => {
    const timer = setTimeout(() => inputRef.current?.focus(), 300);
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
      platform: Platform.OS,
      appVersion: Application.nativeApplicationVersion ?? '1.0.0',
    });

    if (result.ok) {
      const accountId = decodeAccountIdFromJwt(result.value.accessToken);
      if (accountId === null) {
        setScreenState('error');
        setErrorMessage(STRINGS.AUTH.OTP_VERIFY_FAILED);
        return;
      }
      await signIn({
        accessToken: result.value.accessToken,
        refreshToken: result.value.refreshToken,
        accountId,
      });
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

  // Auto-submit on 6th digit
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
    <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
      >
        {/* Back button */}
        <TouchableOpacity
          style={styles.backButton}
          onPress={() => router.back()}
          hitSlop={12}
          accessibilityRole="button"
          accessibilityLabel="Back to phone entry"
        >
          <ArrowLeft size={24} color={Colors.foreground} strokeWidth={2} />
        </TouchableOpacity>

        <View style={styles.container}>
          <Text style={styles.title}>{STRINGS.AUTH.OTP_TITLE}</Text>
          <Text style={styles.subtitle}>
            {STRINGS.AUTH.OTP_SUBTITLE_PREFIX}
            <Text style={styles.destinationText}>{maskedDestination}</Text>
          </Text>

          {/* Hidden input captures the keyboard */}
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
            accessibilityLabel={STRINGS.AUTH.OTP_INPUT_LABEL}
            editable={screenState !== 'loading'}
          />

          {/* Visible OTP digit boxes */}
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
              color={Colors.primary}
              size="small"
              accessibilityLabel={STRINGS.AUTH.OTP_VERIFYING}
            />
          )}

          {screenState === 'error' && errorMessage !== '' && (
            <Text
              style={styles.errorText}
              accessibilityRole="alert"
              accessibilityLiveRegion="polite"
            >
              {errorMessage}
            </Text>
          )}

          {/* Manual submit fallback */}
          {screenState !== 'loading' && otp.length === OTP_LENGTH && (
            <Button
              label={STRINGS.AUTH.OTP_SUBMIT}
              onPress={handleSubmit}
              accessibilityLabel={STRINGS.AUTH.OTP_SUBMIT_LABEL}
            />
          )}

          <TouchableOpacity
            style={styles.resendContainer}
            onPress={() => router.back()}
            accessibilityRole="button"
            accessibilityLabel={STRINGS.AUTH.OTP_RESEND}
          >
            <Text style={styles.resendText}>{STRINGS.AUTH.OTP_RESEND}</Text>
          </TouchableOpacity>
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: Colors.card },
  flex: { flex: 1 },
  backButton: {
    paddingHorizontal: ScreenPaddingH,
    paddingTop: Spacing.md,
    paddingBottom: Spacing.xs,
    alignSelf: 'flex-start',
  },
  container: {
    flex: 1,
    paddingHorizontal: ScreenPaddingH,
    paddingTop: Spacing['2xl'],
    paddingBottom: Spacing['3xl'],
    alignItems: 'center',
  },
  title: {
    fontSize: 28,
    fontFamily: FontFamily.bold,
    color: Colors.foreground,
    marginBottom: Spacing.sm,
    alignSelf: 'flex-start',
  },
  subtitle: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
    marginBottom: Spacing['3xl'],
    lineHeight: FontSize.body * 1.5,
    alignSelf: 'flex-start',
  },
  destinationText: {
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
  },
  hiddenInput: {
    position: 'absolute',
    opacity: 0,
    height: 0,
    width: 0,
  },
  otpBoxRow: {
    flexDirection: 'row',
    gap: Spacing.sm + 2,
    marginBottom: Spacing['2xl'],
  },
  otpBox: {
    width: 48,
    height: 56,
    borderWidth: 1.5,
    borderColor: Colors.border,
    borderRadius: Radius.sm,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: Colors.muted,
  },
  otpBoxActive: {
    borderColor: Colors.primary,
    backgroundColor: Colors.primarySubtle,
  },
  otpBoxError: {
    borderColor: Colors.destructive,
    backgroundColor: Colors.destructiveSubtle,
  },
  otpChar: {
    fontSize: 22,
    fontFamily: FontFamily.bold,
    color: Colors.foreground,
  },
  loadingIndicator: { marginVertical: Spacing.sm },
  errorText: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.destructive,
    marginBottom: Spacing.lg,
    textAlign: 'center',
  },
  resendContainer: {
    marginTop: Spacing.md,
    padding: Spacing.sm,
  },
  resendText: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.medium,
    color: Colors.primary,
  },
});
