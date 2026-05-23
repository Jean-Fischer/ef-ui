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
    var surface = container.closest('.efui-surface') || document;
    var fallback = surface.querySelector('[data-role="efui-table-fallback"]');
    var statusHost = surface.querySelector('[data-role="efui-table-status"]');
    if (!(configElement instanceof HTMLScriptElement) || !(host instanceof HTMLElement)) {
      return;
    }

    function clearIndexedQuery(params, prefix, suffixes) {
      Array.from(params.keys()).forEach(function (key) {
        if (key.indexOf(prefix + '.') !== 0) {
          return;
        }

        suffixes.forEach(function (suffix) {
          if (key.endsWith('.' + suffix)) {
            params.delete(key);
          }
        });
      });
    }

    function clearFilterQuery(params) {
      clearIndexedQuery(params, 'filter', ['field', 'op', 'value']);
    }

    function isBlankFilterValue(value) {
      return value === null || value === undefined || String(value).trim() === '';
    }

    function replaceBrowserUrl(targetListUrl, params) {
      var query = params.toString();
      window.history.replaceState({}, '', targetListUrl + (query ? '?' + query : ''));
    }

    function escapeHtml(value) {
      return String(value || '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    }

    function renderStatus(status) {
      if (!(statusHost instanceof HTMLElement) || !status) {
        return;
      }

      var warnings = Array.isArray(status.warnings) ? status.warnings : [];
      var errors = Array.isArray(status.errors) ? status.errors : [];
      var items = Array.isArray(status.items) ? status.items : [];
      var html = '';
      var hasContent = warnings.length > 0 || errors.length > 0 || items.length > 0;
      if (!hasContent) {
        statusHost.innerHTML = '';
        return;
      }

      if (warnings.length > 0) {
        html += '<div class="efui-warning-summary" data-role="efui-table-status-warnings">'
          + warnings.map(function (warning) {
              return '<div class="efui-warning">' + escapeHtml(warning) + '</div>';
            }).join('')
          + '</div>';
      }

      if (errors.length > 0) {
        html += '<div class="efui-error-summary" data-role="efui-table-status-errors">'
          + errors.map(function (error) {
              return '<div class="efui-error">' + escapeHtml(error) + '</div>';
            }).join('')
          + '</div>';
      }

      if (items.length > 0) {
        html += '<div class="efui-table-status-items" data-role="efui-table-status-items">'
          + items.map(function (item) {
              return '<div class="efui-table-status-item">' + escapeHtml(item) + '</div>';
            }).join('')
          + '</div>';
      }

      statusHost.setAttribute('data-offset', String(status.offset || 0));
      statusHost.setAttribute('data-limit', String(status.limit || 0));
      statusHost.innerHTML = html;
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

    function readInitialSort(sorts) {
      return readQuerySortState(sorts)
        .map(function (sort) {
          return { column: sort.field, dir: sort.dir };
        });
    }

    function readQuerySortState(sorts) {
      return (sorts || [])
        .map(function (sort) {
          var field = sort && (sort.field || sort.Field || (sort.column && typeof sort.column.getField === 'function' ? sort.column.getField() : ''));
          var direction = (sort && (sort.dir || sort.direction || sort.Direction) || '').toLowerCase();
          if (!field || !direction) {
            return null;
          }

          return { field: field, dir: direction };
        })
        .filter(function (sort) { return sort !== null; });
    }

    function readInitialHeaderFilter(columns) {
      return (columns || [])
        .filter(function (column) {
          return !!column
            && column.field !== '__actions'
            && column.headerFilter
            && !isBlankFilterValue(column.headerFilterValue);
        })
        .map(function (column) {
          return {
            field: column.field,
            value: String(column.headerFilterValue)
          };
        });
    }

    function compareCellValues(left, right) {
      var leftValue = left && typeof left === 'object' && 'text' in left ? left.text : left;
      var rightValue = right && typeof right === 'object' && 'text' in right ? right.text : right;
      return String(leftValue || '').localeCompare(String(rightValue || ''), undefined, { numeric: true, sensitivity: 'base' });
    }

    function getDisplayText(value) {
      return value && typeof value === 'object' && 'text' in value ? value.text : value;
    }

    function matchesHeaderFilter(column, rowValue, headerValue) {
      var filterValue = String(headerValue ?? '');
      if (filterValue.length === 0) {
        return true;
      }

      var candidate = String(getDisplayText(rowValue) ?? '');
      var operator = String(column.filterOperator || 'contains').toLowerCase();
      if (operator === 'eq') {
        return candidate.localeCompare(filterValue, undefined, { numeric: true, sensitivity: 'base' }) === 0;
      }

      return candidate.toLowerCase().includes(filterValue.toLowerCase());
    }

    function createColumns(columns) {
      return (columns || []).map(function (column) {
        var isActionsColumn = column.field === '__actions';

        return {
          title: column.title || column.field || '',
          field: column.field,
          headerSort: column.headerSort !== false,
          headerSortTristate: column.headerSort !== false,
          headerFilter: column.headerFilter === false ? false : column.headerFilter,
          headerFilterLiveFilter: column.headerFilterLiveFilter !== false,
          headerFilterFunc: isActionsColumn || column.headerFilter === false
            ? undefined
            : function (headerValue, rowValue) {
                return matchesHeaderFilter(column, rowValue, headerValue);
              },
          sorter: isActionsColumn || column.headerSort === false
            ? undefined
            : function (left, right) {
                return compareCellValues(left, right);
              },
          cssClass: isActionsColumn ? 'efui-tabulator-actions-column' : 'efui-tabulator-data-column',
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
          }
        };
      });
    }

    var table = new window.Tabulator(host, {
      data: config.rows || [],
      columns: createColumns(config.columns),
      layout: 'fitColumns',
      reactiveData: false,
      initialSort: readInitialSort(config.query && config.query.sorts),
      initialHeaderFilter: readInitialHeaderFilter(config.columns),
      dataLoader: true,
      dataLoaderLoading: '<div class="efui-tabulator-loader">Loading table…</div>',
      dataLoaderError: '<div class="efui-tabulator-loader efui-tabulator-loader-error">Unable to load table.</div>'
    });

    window.setTimeout(function () {
      renderStatus(config.status);
      container.classList.add('efui-table-enhancement-ready');
      if (fallback instanceof HTMLElement) {
        fallback.classList.add('efui-table-fallback-hidden');
      }
    }, 0);
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

.efui-tabulator-loader {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 12rem;
  padding: 0.875rem 1rem;
  border-radius: 0.75rem;
  background: #eff6ff;
  color: #1d4ed8;
  font-weight: 600;
}

.efui-tabulator-loader-error {
  background: #fef2f2;
  color: #b91c1c;
}

.efui-table-fallback-hidden {
  display: none;
}

.efui-tabulator-actions-column {
  min-width: 11rem;
}
""";
}
