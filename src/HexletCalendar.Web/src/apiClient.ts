import { client } from '../../HexletCalendar.Contracts/generated/sdk/client.gen'

client.setConfig({
  baseUrl: window.location.origin,
  fetch: async (input) => {
    const request = input as Request
    return fetch(request.url.slice(window.location.origin.length), {
    method: request.method,
    headers: {
      'Content-Type': request.headers.get('Content-Type') ?? 'application/json',
      ...(request.headers.has('Idempotency-Key') ? { 'Idempotency-Key': request.headers.get('Idempotency-Key')! } : {}),
    },
    body: request.method === 'GET' ? undefined : await request.text(),
    signal: request.signal,
    })
  },
})
