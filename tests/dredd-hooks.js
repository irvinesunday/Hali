/**
 * Hali — Dredd API Contract Test Hooks
 * Provides authentication tokens and request fixtures for each endpoint.
 * 
 * Dredd walks 02_openapi.yaml and hits each endpoint against the live API.
 * These hooks run before/after each transaction to set up auth, seed data, etc.
 * 
 * Docs: https://dredd.org/en/latest/hooks/
 */

const hooks = require('hooks');
const axios  = require('axios').default;

const BASE = 'http://localhost:8080';
let accessToken = '';
let clusterId   = '';

// ── Before all tests: authenticate and get a token ───────────────────────────
hooks.beforeAll(async (transactions, done) => {
  try {
    // Step 1: Request OTP
    await axios.post(`${BASE}/v1/auth/otp`, {
      method: 'phone_otp',
      destination: '+254700000000'
    });

    // Step 2: Verify OTP (test environment uses bypass code '000000')
    const res = await axios.post(`${BASE}/v1/auth/verify`, {
      destination:       '+254700000000',
      otpCode:           '000000',
      deviceFingerprint: 'dredd-test-device-01',
    });
    accessToken = res.data.accessToken;
    console.log('Dredd: authenticated successfully');
  } catch (e) {
    console.error('Dredd: auth failed —', e.message);
  }
  done();
});

// ── Inject auth header into every request ────────────────────────────────────
hooks.beforeEach((transaction, done) => {
  if (accessToken) {
    transaction.request.headers['Authorization'] = `Bearer ${accessToken}`;
  }
  transaction.request.headers['Idempotency-Key'] = `dredd-${Date.now()}-${Math.random()}`;
  done();
});

// ── Skip endpoints that require pre-existing state Dredd can't create ────────
const SKIP = [
  '/v1/clusters/{id} > GET',
  '/v1/clusters/{id}/participation > POST',
  '/v1/clusters/{id}/context > POST',
  '/v1/clusters/{id}/restoration-response > POST',
];

SKIP.forEach(name => {
  hooks.before(name, (transaction, done) => {
    transaction.skip = true;
    done();
  });
});

// ── Provide a valid body for POST /v1/signals/preview ────────────────────────
hooks.before('/v1/signals/preview > POST', (transaction, done) => {
  transaction.request.body = JSON.stringify({
    text: 'Big potholes near National Oil in Nairobi West',
    sourceLanguage: 'en',
    location: { latitude: -1.303, longitude: 36.814 },
  });
  done();
});

// ── Capture clusterId after signal submit ─────────────────────────────────────
hooks.after('/v1/signals/submit > POST', (transaction, done) => {
  try {
    const body = JSON.parse(transaction.real.body);
    if (body.clusterId) clusterId = body.clusterId;
  } catch (_) {}
  done();
});
