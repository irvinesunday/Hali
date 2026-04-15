// apps/citizen-mobile/src/config/strings.ts
//
// All user-visible strings in one place.
// No hardcoded user-facing strings in screen components — import from here.

export const STRINGS = {
  AUTH: {
    // Phone entry screen
    PHONE_TITLE: 'Enter your phone number',
    PHONE_SUBTITLE:
      "We'll send a one-time code to verify your number.",
    PHONE_PLACEHOLDER: '7XX XXX XXX',
    PHONE_INPUT_LABEL: 'Phone number input',
    PHONE_INPUT_HINT: 'Enter your Kenyan phone number without the country code',
    PHONE_SUBMIT: 'Send code',
    PHONE_SUBMIT_LABEL: 'Send one-time code',
    PHONE_NOTE:
      'Standard SMS rates may apply. Your number is only used for sign-in.',
    OTP_REQUEST_FAILED:
      'Unable to send a code right now. Please try again.',
    OTP_RATE_LIMIT:
      'Too many requests. Please wait a moment before trying again.',

    // OTP verification screen
    OTP_TITLE: 'Enter your code',
    OTP_SUBTITLE_PREFIX: 'We sent a 6-digit code to ',
    OTP_INPUT_LABEL: 'Six digit one-time code input',
    OTP_SUBMIT: 'Verify',
    OTP_SUBMIT_LABEL: 'Verify one-time code',
    OTP_VERIFYING: 'Verifying your code…',
    OTP_INVALID: 'That code is incorrect. Please try again.',
    OTP_VERIFY_FAILED:
      'Verification failed. Please check your code and try again.',
    OTP_MISSING_DESTINATION:
      'Something went wrong. Please go back and enter your phone number again.',
    OTP_RESEND: "Didn't receive a code? Go back",
    OTP_RESEND_WAIT: 'Resend available in',
  },
} as const;
