import { currentUser } from '@clerk/nextjs/server';
import { NextRequest, NextResponse } from 'next/server';

function verificationIsComplete(value: { verification?: { status?: string | null } | null } | undefined) {
  return value?.verification?.status === 'verified';
}

function providerFor(user: Awaited<ReturnType<typeof currentUser>>, hasVerifiedPhone: boolean) {
  if (!user) return 'email_otp';
  const provider = user.externalAccounts?.find(account => account.verification?.status === 'verified')?.provider?.toLowerCase() ?? '';
  if (provider.includes('google')) return 'google';
  if (provider.includes('apple')) return 'apple';
  if (hasVerifiedPhone) return 'phone';
  return 'email_otp';
}

export async function POST(request: NextRequest) {
  if (!process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY || !process.env.CLERK_SECRET_KEY)
    return NextResponse.json({ error: 'Verified identity is not configured.' }, { status: 503 });

  const bridgeSecret = process.env.AUTH_BRIDGE_SECRET;
  const apiBase = process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, '');
  if (!bridgeSecret || !apiBase)
    return NextResponse.json({ error: 'FijiLaw identity bridge configuration is incomplete.' }, { status: 503 });

  const user = await currentUser();
  if (!user) return NextResponse.json({ error: 'Sign in or register before continuing.' }, { status: 401 });

  const primaryEmail = user.emailAddresses.find(item => item.id === user.primaryEmailAddressId) ?? user.emailAddresses.find(verificationIsComplete);
  const primaryPhone = user.phoneNumbers.find(item => item.id === user.primaryPhoneNumberId) ?? user.phoneNumbers.find(verificationIsComplete);
  const emailVerified = Boolean(primaryEmail && verificationIsComplete(primaryEmail));
  const phoneVerified = Boolean(primaryPhone && verificationIsComplete(primaryPhone));
  const phoneNumber = phoneVerified ? primaryPhone?.phoneNumber ?? null : null;

  if (phoneVerified && phoneNumber && !/^\+679\d{7}$/.test(phoneNumber))
    return NextResponse.json({ error: 'Mobile registration is limited to Fiji +679 numbers.' }, { status: 400 });
  if (!emailVerified && !phoneVerified)
    return NextResponse.json({ error: 'Complete the email or mobile verification code before continuing.' }, { status: 403 });

  const body = await request.json().catch(() => ({} as { requestedPlanCode?: string }));
  const requestedPlanCode = typeof body?.requestedPlanCode === 'string' ? body.requestedPlanCode : 'free';
  const identityProvider = providerFor(user, phoneVerified);

  const response = await fetch(`${apiBase}/api/auth/external-session`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Auth-Bridge-Secret': bridgeSecret
    },
    cache: 'no-store',
    body: JSON.stringify({
      identityProvider,
      identitySubject: user.id,
      email: emailVerified ? primaryEmail?.emailAddress ?? null : null,
      phoneNumber,
      emailVerified,
      phoneVerified,
      displayName: [user.firstName, user.lastName].filter(Boolean).join(' ') || user.username || null,
      requestedPlanCode
    })
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok)
    return NextResponse.json({ error: payload?.error ?? payload?.detail ?? 'FijiLaw account linking could not be completed.' }, { status: response.status });

  return NextResponse.json({
    ...payload,
    primaryIdentifier: phoneVerified && phoneNumber ? phoneNumber : primaryEmail?.emailAddress ?? payload.email,
    identityProvider
  });
}
