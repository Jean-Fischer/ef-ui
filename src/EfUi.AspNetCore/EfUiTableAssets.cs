namespace EfUi.AspNetCore;

internal static class EfUiTableAssets
{
    internal const string ScriptContent = """
document.addEventListener('DOMContentLoaded', function () {
  document.querySelectorAll('[data-role="efui-table-enhancement"]').forEach(function (container) {
    if (!(container instanceof HTMLElement)) {
      return;
    }

    var configElement = container.querySelector('[data-role="efui-table-config"]');
    var host = container.querySelector('[data-role="efui-table-host"]');
    var loading = container.querySelector('[data-role="efui-table-loading"]');
    var surface = container.closest('.efui-surface') || document;
    var fallback = surface.querySelector('[data-role="efui-table-fallback"]');
    if (!(configElement instanceof HTMLScriptElement) || !(host instanceof HTMLElement)) {
      return;
    }

    function setLoading(isLoading, message) {
      if (!(loading instanceof HTMLElement)) {
        return;
      }

      loading.hidden = !isLoading;
      if (message) {
        loading.textContent = message;
      }
    }

    function navigate(mutator) {
      var params = new URLSearchParams(window.location.search);
      mutator(params);
      params.set('offset', '0');
      setLoading(true, 'Loading table…');
      var query = params.toString();
      window.location.assign(window.location.pathname + (query ? '?' + query : ''));
    }

    var config;
    try {
      config = JSON.parse(configElement.textContent || '{}');
    } catch {
      return;
    }

    if (typeof window.Tabulator !== 'function') {
      return;
    }

    function readSort(sort) {
      if (!sort || typeof sort !== 'object') {
        return { field: '', direction: '' };
      }

      return {
        field: sort.field || sort.Field || '',
        direction: (sort.direction || sort.Direction || '').toLowerCase()
      };
    }

    var activeSort = readSort((config.query && config.query.sorts && config.query.sorts[0]) || null);

    var columns = (config.columns || []).map(function (column) {
      var isActionsColumn = column.field === '__actions';
      var direction = activeSort.field === column.field ? activeSort.direction : '';
      var title = column.title || column.field || '';
      if (direction === 'asc') {
        title += ' ↑';
      } else if (direction === 'desc') {
        title += ' ↓';
      }

      return {
        title: title,
        field: column.field,
        headerSort: false,
        cssClass: isActionsColumn ? 'efui-tabulator-actions-column' : (column.headerSort === false ? '' : 'efui-tabulator-sortable'),
        formatter: function (cell) {
          var value = cell.getValue();
          if (isActionsColumn) {
            var wrapper = document.createElement('div');
            wrapper.className = 'efui-row-actions';
            wrapper.innerHTML = typeof value === 'string'
              ? value
              : (value && value.html ? value.html : '');
            return wrapper;
          }

          if (value && value.href) {
            var anchor = document.createElement('a');
            anchor.className = 'efui-cell-link';
            anchor.href = value.href;
            anchor.textContent = value.text || '';
            return anchor;
          }

          return value && value.text ? value.text : '';
        },
        headerClick: isActionsColumn || column.headerSort === false
          ? undefined
          : function () {
              navigate(function (params) {
                var currentField = params.get('sort.0.field') || activeSort.field;
                var currentDir = (params.get('sort.0.dir') || activeSort.direction || '').toLowerCase();

                if (currentField === column.field && currentDir === 'asc') {
                  params.set('sort.0.field', column.field);
                  params.set('sort.0.dir', 'desc');
                  return;
                }

                if (currentField === column.field && currentDir === 'desc') {
                  params.delete('sort.0.field');
                  params.delete('sort.0.dir');
                  return;
                }

                params.set('sort.0.field', column.field);
                params.set('sort.0.dir', 'asc');
              });
            }
      };
    });

    setLoading(true, 'Loading table…');

    new window.Tabulator(host, {
      data: config.rows || [],
      columns: columns,
      layout: 'fitColumns',
      reactiveData: false
    });

    setLoading(false, '');
    container.classList.add('efui-table-enhancement-ready');
    if (fallback instanceof HTMLElement) {
      fallback.classList.add('efui-table-fallback-hidden');
    }
  });
});
""";

    internal const string StylesheetContent = """
.efui-table-enhancement {
  position: relative;
  margin-bottom: 1rem;
}

.efui-table-host {
  min-height: 12rem;
}

.efui-table-loading {
  margin-bottom: 0.75rem;
  padding: 0.75rem 0.875rem;
  border: 1px solid #bfdbfe;
  border-radius: 0.625rem;
  background: #eff6ff;
  color: #1d4ed8;
  font-weight: 600;
}

.efui-table-fallback-hidden {
  display: none;
}

.efui-tabulator-sortable .tabulator-col-title {
  color: #2563eb;
  cursor: pointer;
}

.efui-tabulator-actions-column {
  min-width: 11rem;
}
""";
}
