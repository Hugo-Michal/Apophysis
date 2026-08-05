const scheme = document.querySelector('#scheme');
const generate = document.querySelector('#generate');
const download = document.querySelector('#download');
const preview = document.querySelector('#preview');
const empty = document.querySelector('#empty');
const status = document.querySelector('#status');
let latestXml = '';
let latestName = 'flame';

generate.addEventListener('click', async () => {
  generate.disabled = true;
  status.textContent = 'Searching for a distinct form...';
  try {
    const response = await fetch('/api/generate', {
      method: 'POST', headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({scheme: scheme.value})
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.error || 'Generation failed');
    preview.src = result.preview;
    preview.style.display = 'block';
    empty.style.display = 'none';
    latestXml = result.xml;
    latestName = result.name;
    download.disabled = false;
    status.textContent = `New form found. Novelty ${result.novelty}/10.`;
  } catch (error) {
    status.textContent = error.message;
  } finally {
    generate.disabled = false;
  }
});

download.addEventListener('click', () => {
  const blob = new Blob([latestXml], {type: 'application/xml'});
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = `${latestName}.flame`;
  link.click();
  URL.revokeObjectURL(link.href);
});
