const configuredApiBase = process.env.NEXT_PUBLIC_API_URL?.trim();

export const API_BASE = (configuredApiBase || 'https://fijilaw-api-production-production.up.railway.app').replace(/\/+$/, '');

export const SERVICE_UNAVAILABLE_MESSAGE =
  'FijiLaw AI is temporarily unable to reach the legal service. Your information has not been submitted. Please try again shortly.';

export async function fetchWithTimeout(input: RequestInfo | URL, init: RequestInit = {}, timeoutMs = 20000) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(input, { ...init, signal: controller.signal });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new Error('The legal service took too long to respond. Please try again.');
    }
    throw new Error(SERVICE_UNAVAILABLE_MESSAGE);
  } finally {
    clearTimeout(timeout);
  }
}

export async function checkApiHealth() {
  try {
    const response = await fetchWithTimeout(`${API_BASE}/health`, { cache: 'no-store' }, 8000);
    if (!response.ok) return false;
    const body = await response.json();
    return body?.status === 'ok';
  } catch {
    return false;
  }
}

export async function readApiError(response: Response, fallback: string) {
  try {
    const body = await response.json();
    return body?.error ?? body?.detail ?? body?.title ?? fallback;
  } catch {
    return fallback;
  }
}
