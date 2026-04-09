/**
 * Async generator that yields parsed JSON objects from an NDJSON HTTP response.
 * Each line of the response body must be a valid JSON value.
 */
export async function* readNdjsonStream<T>(
  response: Response
): AsyncGenerator<T> {
  if (!response.body) {
    throw new Error('Response has no body');
  }
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop() ?? '';
      for (const line of lines) {
        const trimmed = line.trim();
        if (trimmed) yield JSON.parse(trimmed) as T;
      }
    }
    const trailing = buffer.trim();
    if (trailing) yield JSON.parse(trailing) as T;
  } finally {
    reader.releaseLock();
  }
}
