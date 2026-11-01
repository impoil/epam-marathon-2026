function handler(event) {
  var request = event.request;
  if (!request.uri.split('/').pop().includes('.')) request.uri = '/index.html';
  return request;
}
