// Set the theme before first paint to avoid a flash.
try {
  document.documentElement.dataset.theme = localStorage.getItem('panda.theme') ||
    (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
} catch (e) {}
