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

.efui-page {
  max-width: 72rem;
  margin: 0 auto;
  padding: 2rem 1.25rem 3rem;
}

.efui-form-page {
  max-width: 48rem;
  margin: 0 auto;
  padding: 2rem 1.25rem 3rem;
}

.efui-surface,
.efui-form {
  background: #ffffff;
  border: 1px solid #e5e7eb;
  border-radius: 0.75rem;
  box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04);
}

.efui-surface {
  padding: 1.5rem;
}

.efui-form {
  padding: 1.5rem;
}

.efui-surface > h1,
.efui-form-title {
  margin: 0 0 1.5rem;
  font-size: 1.5rem;
  font-weight: 700;
}

.efui-index-list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.efui-link-grid {
  display: grid;
  gap: 0.75rem;
  grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
}

.efui-link-grid li {
  margin: 0;
}

.efui-link-grid a {
  display: block;
  padding: 1rem;
  border: 1px solid #e5e7eb;
  border-radius: 0.75rem;
  background: #f8fafc;
  color: #111827;
  text-decoration: none;
  font-weight: 600;
}

.efui-link-grid a:hover {
  border-color: #bfdbfe;
  background: #eff6ff;
  color: #1d4ed8;
}

.efui-breadcrumbs {
  margin: 0 0 1rem;
}

.efui-breadcrumb-list {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.375rem;
  list-style: none;
  margin: 0;
  padding: 0;
  color: #6b7280;
  font-size: 0.875rem;
}

.efui-breadcrumb-item {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
}

.efui-breadcrumb-item + .efui-breadcrumb-item::before {
  content: "/";
  color: #9ca3af;
}

.efui-breadcrumb-link {
  color: #2563eb;
  text-decoration: none;
  font-weight: 600;
}

.efui-breadcrumb-link:hover {
  text-decoration: underline;
}

.efui-breadcrumb-current {
  color: #374151;
  font-weight: 600;
}

.efui-page-actions {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 1rem;
}

.efui-table-status {
  display: grid;
  gap: 0.5rem;
  margin-bottom: 1rem;
  padding: 0.75rem 1rem;
  border: 1px solid #e5e7eb;
  border-radius: 0.75rem;
  background: #f8fafc;
}

.efui-table-status .efui-error-summary {
  margin-bottom: 0;
}

.efui-table-status-items {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.efui-table-status-item,
.efui-table-status-empty {
  display: inline-flex;
  align-items: center;
  padding: 0.375rem 0.625rem;
  border: 1px solid #d1d5db;
  border-radius: 999px;
  background: #ffffff;
  color: #374151;
  font-size: 0.875rem;
}

.efui-table-enhancement {
  margin-bottom: 1rem;
}

.efui-table-host {
  min-height: 12rem;
}

.tabulator {
  border: 1px solid #e5e7eb;
  border-radius: 0.75rem;
  overflow: hidden;
  background: #ffffff;
  font: inherit;
}

.tabulator .tabulator-header {
  border-bottom: 1px solid #e5e7eb;
  background: #f8fafc;
}

.tabulator .tabulator-header .tabulator-col {
  background: #f8fafc;
  border-right-color: #e5e7eb;
}

.tabulator .tabulator-header .tabulator-col.tabulator-sortable {
  color: #2563eb;
}

.tabulator .tabulator-header .tabulator-col.tabulator-sortable .tabulator-col-title {
  cursor: pointer;
  font-weight: 700;
}

.tabulator .tabulator-header .tabulator-header-filter {
  margin-top: 0.5rem;
}

.tabulator .tabulator-header .tabulator-header-filter input {
  width: 100%;
  box-sizing: border-box;
  padding: 0.5rem 0.625rem;
  border: 1px solid #d1d5db;
  border-radius: 0.5rem;
  background: #ffffff;
  color: #111827;
  font: inherit;
}

.tabulator .tabulator-tableholder .tabulator-row {
  background: #ffffff;
}

.tabulator .tabulator-tableholder .tabulator-row:nth-child(even) {
  background: #f9fafb;
}

.tabulator .tabulator-tableholder .tabulator-row:hover {
  background: #eff6ff;
}

.efui-cell-link {
  color: #2563eb;
  text-decoration: none;
  font-weight: 600;
}

.efui-cell-link:hover {
  text-decoration: underline;
}

.efui-primary-link {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 0.625rem;
  background: #111827;
  color: #ffffff;
  padding: 0.7rem 1rem;
  font-weight: 600;
  text-decoration: none;
}

.efui-primary-link:hover {
  background: #1f2937;
}

.efui-table-wrapper {
  overflow-x: auto;
  border: 1px solid #e5e7eb;
  border-radius: 0.75rem;
  background: #ffffff;
}

.efui-table {
  width: 100%;
  border-collapse: separate;
  border-spacing: 0;
}

.efui-table th,
.efui-table td {
  padding: 0.875rem 1rem;
  border-bottom: 1px solid #e5e7eb;
  text-align: left;
  vertical-align: top;
}

.efui-table th {
  background: #f8fafc;
  color: #374151;
  font-size: 0.875rem;
  font-weight: 700;
}

.efui-table tbody tr:nth-child(even) {
  background: #f9fafb;
}

.efui-table tbody tr:hover {
  background: #eff6ff;
}

.efui-table tbody tr:last-child td {
  border-bottom: 0;
}

.efui-row-actions {
  white-space: nowrap;
}

.efui-row-action-link,
.efui-row-action-form {
  display: inline-flex;
  vertical-align: middle;
}

.efui-row-action-link {
  margin-right: 0.5rem;
  color: #2563eb;
  text-decoration: none;
  font-weight: 600;
}

.efui-row-action-link:hover {
  text-decoration: underline;
}

.efui-row-action-form {
  margin: 0;
}

.efui-row-action-button {
  border: 1px solid #fecaca;
  border-radius: 0.5rem;
  background: #ffffff;
  color: #b91c1c;
  padding: 0.45rem 0.75rem;
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}

.efui-row-action-button:hover {
  background: #fef2f2;
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
.efui-search-input:focus,
.efui-primary-link:focus,
.efui-row-action-button:focus,
.efui-link-grid a:focus,
.efui-row-action-link:focus,
.efui-related-link-action:focus,
.efui-breadcrumb-link:focus {
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
