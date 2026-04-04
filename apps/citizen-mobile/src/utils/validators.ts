export function isValidE164(phone: string): boolean {
  return /^\+[1-9]\d{7,14}$/.test(phone.replace(/\s/g, ''));
}

export function normalisePhone(input: string, countryCode = 'KE'): string {
  const stripped = input.replace(/\s|-/g, '');
  if (stripped.startsWith('+')) return stripped;

  // Kenya: 07XXXXXXXX → +2547XXXXXXXX
  if (countryCode === 'KE' && stripped.startsWith('0')) {
    return '+254' + stripped.slice(1);
  }
  return stripped;
}
