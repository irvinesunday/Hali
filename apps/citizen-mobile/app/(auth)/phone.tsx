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
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { router } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { requestOtp } from '../../src/api/auth';
import { Button } from '../../src/components/common/Button';
import { STRINGS } from '../../src/config/strings';
import { normaliseKenyaPhone, isValidKenyaPhoneInput } from '../../src/utils/phone';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
  ScreenPaddingH,
} from '../../src/theme';

type ScreenState = 'idle' | 'loading' | 'error';
const KENYA_PREFIX = '+254';

export default function PhoneScreen(): React.ReactElement {
  const [phoneInput, setPhoneInput] = useState('');
  const [screenState, setScreenState] = useState<ScreenState>('idle');
  const [errorMessage, setErrorMessage] = useState('');

  const isSubmittable = screenState !== 'loading' && isValidKenyaPhoneInput(phoneInput);

  async function handleSubmit(): Promise<void> {
    const destination = normaliseKenyaPhone(phoneInput);
    if (destination === null) return;

    setScreenState('loading');
    setErrorMessage('');

    const result = await requestOtp({ destination, authMethod: 'phone_otp' });

    if (result.ok) {
      setScreenState('idle');
      router.push({ pathname: '/(auth)/otp', params: { destination } });
    } else {
      setScreenState('error');
      setErrorMessage(
        result.error.status === 429
          ? STRINGS.AUTH.OTP_RATE_LIMIT
          : result.error.message || STRINGS.AUTH.OTP_REQUEST_FAILED,
      );
    }
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
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
              placeholderTextColor={Colors.faintForeground}
              maxLength={12}
              returnKeyType="done"
              onSubmitEditing={isSubmittable ? handleSubmit : undefined}
              autoFocus
              accessibilityLabel={STRINGS.AUTH.PHONE_INPUT_LABEL}
              accessibilityHint={STRINGS.AUTH.PHONE_INPUT_HINT}
              editable={screenState !== 'loading'}
            />
          </View>

          {screenState === 'error' && errorMessage !== '' && (
            <Text
              style={styles.errorText}
              accessibilityRole="alert"
              accessibilityLiveRegion="polite"
            >
              {errorMessage}
            </Text>
          )}

          <Button
            label={STRINGS.AUTH.PHONE_SUBMIT}
            onPress={handleSubmit}
            disabled={!isSubmittable}
            loading={screenState === 'loading'}
          />

          <Text style={styles.note}>{STRINGS.AUTH.PHONE_NOTE}</Text>
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: Colors.card },
  flex: { flex: 1 },
  container: {
    flex: 1,
    paddingHorizontal: ScreenPaddingH,
    paddingTop: Spacing['4xl'],
    paddingBottom: Spacing['3xl'],
  },
  title: {
    fontSize: 28,
    fontFamily: FontFamily.bold,
    color: Colors.foreground,
    marginBottom: Spacing.sm,
  },
  subtitle: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
    marginBottom: Spacing['3xl'],
    lineHeight: FontSize.body * 1.5,
  },
  inputRow: {
    flexDirection: 'row',
    borderWidth: 1.5,
    borderColor: Colors.border,
    borderRadius: Radius.md,
    overflow: 'hidden',
    marginBottom: Spacing.sm,
  },
  prefixContainer: {
    backgroundColor: Colors.muted,
    paddingHorizontal: Spacing.lg,
    paddingVertical: Spacing.lg,
    justifyContent: 'center',
    borderRightWidth: 1,
    borderRightColor: Colors.border,
  },
  prefixText: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
  },
  input: {
    flex: 1,
    fontSize: 18,
    fontFamily: FontFamily.regular,
    paddingHorizontal: Spacing.lg,
    paddingVertical: Spacing.lg,
    color: Colors.foreground,
    letterSpacing: 0.5,
  },
  errorText: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.destructive,
    marginBottom: Spacing.md,
    marginTop: Spacing.xs,
  },
  note: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.faintForeground,
    marginTop: Spacing.xl,
    textAlign: 'center',
    lineHeight: FontSize.bodySmall * 1.5,
  },
});
