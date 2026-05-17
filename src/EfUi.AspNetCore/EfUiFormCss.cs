namespace EfUi.AspNetCore;

internal static class EfUiFormCss
{
    internal const string Content = """
.efui-body {
  margin: 0;
  background: #f8fafc;
  color: #111827;
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}

.efui-form-page {
  max-width: 48rem;
  margin: 0 auto;
  padding: 2rem 1.25rem 3rem;
}

.efui-form {
  background: #ffffff;
  border: 1px solid #e5e7eb;
  border-radius: 0.75rem;
  padding: 1.5rem;
  box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04);
}

.efui-form-title {
  margin: 0 0 1.5rem;
  font-size: 1.5rem;
  font-weight: 700;
}

.efui-field {
  display: grid;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.efui-label {
  font-size: 0.95rem;
  font-weight: 600;
}

.efui-readonly-value {
  min-height: 1.5rem;
  color: #374151;
}

.efui-input,
.efui-select,
.efui-search-input {
  width: 100%;
  box-sizing: border-box;
  padding: 0.625rem 0.75rem;
  border: 1px solid #d1d5db;
  border-radius: 0.625rem;
  background: #ffffff;
  color: #111827;
}

.efui-input:focus,
.efui-select:focus,
.efui-search-input:focus {
  outline: 2px solid #bfdbfe;
  outline-offset: 1px;
  border-color: #2563eb;
}

.efui-error-summary {
  display: grid;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.efui-error {
  padding: 0.75rem 0.875rem;
  border: 1px solid #fecaca;
  border-radius: 0.625rem;
  background: #fef2f2;
  color: #b91c1c;
}

.efui-chip-picker {
  display: grid;
  gap: 0.75rem;
}

.efui-chip-picker-selected,
.efui-chip-picker-results,
.efui-chip-picker-fallback {
  display: grid;
  gap: 0.5rem;
}

.efui-chip-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  min-height: 2.5rem;
}

.efui-chip-picker-results,
.efui-chip-picker-fallback {
  max-height: 12rem;
  overflow-y: auto;
  padding: 0.75rem;
  border: 1px solid #e5e7eb;
  border-radius: 0.625rem;
  background: #ffffff;
}

.efui-chip-picker-enhanced .efui-chip-picker-fallback {
  display: none;
}

.efui-chip-picker-option {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  color: #374151;
}

.efui-chip-picker-description {
  color: #6b7280;
}

.efui-chip {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.375rem 0.625rem;
  border: 1px solid #dbeafe;
  border-radius: 999px;
  background: #eff6ff;
  color: #1e3a8a;
}

.efui-chip-remove {
  border: 0;
  background: transparent;
  color: inherit;
  cursor: pointer;
  font-size: 1rem;
  line-height: 1;
  padding: 0;
}

.efui-chip-picker-result {
  width: 100%;
  text-align: left;
  border: 1px solid #e5e7eb;
  border-radius: 0.5rem;
  background: #ffffff;
  color: #111827;
  padding: 0.625rem 0.75rem;
  cursor: pointer;
}

.efui-chip-picker-result:hover {
  background: #f8fafc;
}

.efui-chip-picker-empty {
  color: #6b7280;
  font-size: 0.95rem;
}

.efui-related-links {
  margin: 1.5rem 0;
  padding-top: 1rem;
  border-top: 1px solid #e5e7eb;
}

.efui-related-links-title {
  margin: 0 0 0.75rem;
  font-size: 1rem;
}

.efui-related-link {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.efui-related-link-action {
  color: #2563eb;
  text-decoration: none;
}

.efui-related-link-action:hover {
  text-decoration: underline;
}

.efui-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 0;
  border-radius: 0.625rem;
  background: #111827;
  color: #ffffff;
  padding: 0.7rem 1rem;
  font-weight: 600;
  cursor: pointer;
}
""";
}
