export const ENV_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5119';

export const buildUrl = (baseUrl, path) => {
  return `${baseUrl.replace(/\/$/, '')}${path}`;
};
