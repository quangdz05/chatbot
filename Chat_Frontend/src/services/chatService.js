import { buildUrl } from '../config/apiConfig';

async function parseError(response) {
  const fallback = `HTTP ${response.status}`;
  try {
    const body = await response.json();
    if (body?.message) {
      return `${fallback}: ${body.message}`;
    }
    return `${fallback}: ${JSON.stringify(body)}`;
  } catch {
    return fallback;
  }
}

export const chatService = {
  async ask(baseUrl, message, conversationId = null) {
    const payload = {
      message,
      conversationId: conversationId || null,
    };

    const response = await fetch(buildUrl(baseUrl, '/api/Chat/ask'), {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      throw new Error(await parseError(response));
    }

    return await response.json();
  }
};
