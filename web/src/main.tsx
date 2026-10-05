import { render } from 'preact';
import { App } from './app';
import { I18nProvider } from './i18n';
import './styles.css';

render(
  <I18nProvider>
    <App />
  </I18nProvider>,
  document.getElementById('app')!,
);
