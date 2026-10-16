import test from 'node:test';
import assert from 'node:assert/strict';
import { parseEmailLink } from '../src/lib/auth/email-link.ts';

test('email links preserve encoded Identity token characters', () => {
  const userId = '018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b';
  const token = 'a+/=b&c';
  assert.deepEqual(parseEmailLink(`#userId=${userId}&token=${encodeURIComponent(token)}`), { userId, token });
});
test('invalid, missing and oversized email link credentials are rejected', () => {
  for (const value of ['', '#userId=invalid&token=secret', '#userId=018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b', `#userId=018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b&token=${'a'.repeat(4097)}`]) {
    assert.equal(parseEmailLink(value), null);
  }
});
