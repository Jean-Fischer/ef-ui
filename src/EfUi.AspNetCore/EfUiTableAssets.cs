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

    function clearSortQuery(params) {
      clearIndexedQuery(params, 'sort', ['field', 'dir']);
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
      var emptyMessage = status.emptyMessage || 'No active filters or sorts';
      var html = '';

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

      if (items.length === 0) {
        html += '<div class="efui-table-status-empty" data-role="efui-table-status-empty">' + escapeHtml(emptyMessage) + '</div>';
      } else {
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

    var listUrl = config.listUrl || window.location.pathname;
    var dataUrl = config.dataUrl || (listUrl + '/data');
    var columnMap = {};
    var readyForNavigation = false;
    var applyingResponse = false;
    var pendingRequestId = 0;
    var currentSorters = readQuerySortState(config.query && config.query.sorts);

    function rebuildColumnMap(columns) {
      columnMap = {};
      (columns || []).forEach(function (column) {
        if (column && column.field) {
          columnMap[column.field] = column;
        }
      });
    }

    rebuildColumnMap(config.columns);

    function readInitialSort(sorts) {
      return readQuerySortState(sorts)
        .map(function (sort) {
          return { column: sort.field, dir: sort.dir };
        });
    }

    function readQuerySortState(sorts) {
      return (sorts || [])
        .map(function (sort) {
          var field = sort.field || sort.Field || '';
          var direction = (sort.direction || sort.Direction || '').toLowerCase();
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

    function getFilterOperator(field, value) {
      var column = columnMap[field] || {};
      var defaultOperator = column.filterOperator || 'contains';
      var activeOperator = column.activeFilterOperator;
      var initialValue = column.headerFilterValue;

      if (!activeOperator) {
        return defaultOperator;
      }

      if (isBlankFilterValue(value) || String(value) !== String(initialValue || '')) {
        return defaultOperator;
      }

      return activeOperator;
    }

    function applyFilterQuery(params, filters) {
      clearFilterQuery(params);
      (filters || [])
        .filter(function (filter) {
          return !!filter && !!filter.field && !isBlankFilterValue(filter.value);
        })
        .forEach(function (filter, filterIndex) {
          params.set('filter.' + filterIndex + '.field', filter.field);
          params.set('filter.' + filterIndex + '.op', getFilterOperator(filter.field, filter.value));
          params.set('filter.' + filterIndex + '.value', String(filter.value));
        });
    }

    function applySortQuery(params, sorters) {
      clearSortQuery(params);
      (sorters || [])
        .map(function (sorter) {
          var field = sorter && (sorter.field || (sorter.column && typeof sorter.column.getField === 'function' ? sorter.column.getField() : ''));
          var dir = sorter && sorter.dir;
          return field && dir
            ? { field: field, dir: dir }
            : null;
        })
        .filter(function (sorter) {
          return sorter !== null;
        })
        .forEach(function (sorter, sortIndex) {
          params.set('sort.' + sortIndex + '.field', sorter.field);
          params.set('sort.' + sortIndex + '.dir', sorter.dir);
        });
    }

    function readActiveHeaderFilters(table) {
      if (!table || typeof table.getHeaderFilters !== 'function') {
        return [];
      }

      return table.getHeaderFilters();
    }

    function readTableState(table, sorters) {
      return {
        filters: readActiveHeaderFilters(table),
        sorters: readQuerySortState(sorters || currentSorters)
      };
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
            : function () {
                return true;
              },
          sorter: isActionsColumn || column.headerSort === false
            ? undefined
            : function () {
                return 0;
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

    function syncQueryState(payload) {
      if (!table) {
        return;
      }

      var payloadColumns = payload && payload.columns ? payload.columns : [];
      if (typeof table.setHeaderFilterValue === 'function') {
        payloadColumns
          .filter(function (column) {
            return !!column && column.field !== '__actions';
          })
          .forEach(function (column) {
            table.setHeaderFilterValue(column.field, isBlankFilterValue(column.headerFilterValue) ? '' : String(column.headerFilterValue));
          });
      }

      var sorts = readInitialSort(payload && payload.query && payload.query.sorts);
      if (sorts.length === 0) {
        if (typeof table.clearSort === 'function') {
          table.clearSort();
        } else if (typeof table.setSort === 'function') {
          table.setSort([]);
        }

        return;
      }

      if (typeof table.setSort === 'function') {
        table.setSort(sorts);
      }
    }

    async function applyPayload(payload) {
      config = payload || {};
      listUrl = config.listUrl || listUrl;
      dataUrl = config.dataUrl || dataUrl;
      rebuildColumnMap(config.columns);
      currentSorters = readQuerySortState(config.query && config.query.sorts);

      applyingResponse = true;
      try {
        syncQueryState(config);
        await table.replaceData(config.rows || []);
        renderStatus(config.status);
      } finally {
        applyingResponse = false;
      }
    }

    async function fetchData(params, options) {
      var query = params.toString();
      var requestUrl = dataUrl + (query ? '?' + query : '');
      var requestId = ++pendingRequestId;

      try {
        var response = await fetch(requestUrl, {
          headers: {
            'Accept': 'application/json'
          }
        });

        if (!response.ok) {
          throw new Error('Request failed');
        }

        var payload = await response.json();
        if (requestId !== pendingRequestId) {
          return;
        }

        await applyPayload(payload);
        if (options && options.replaceHistory) {
          replaceBrowserUrl(listUrl, params);
        }
      } catch {
        if (requestId !== pendingRequestId) {
          return;
        }

        await applyPayload(config);
      }
    }

    function requestTableRefresh(mutator, options) {
      var params = new URLSearchParams(window.location.search);
      mutator(params);
      if (!options || options.resetOffset !== false) {
        params.set('offset', '0');
      }

      fetchData(params, {
        replaceHistory: !options || options.replaceHistory !== false
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

    table.on('dataSorting', function (sorters) {
      if (!readyForNavigation || applyingResponse) {
        return;
      }

      var state = readTableState(table, sorters);
      currentSorters = state.sorters;
      requestTableRefresh(function (params) {
        applySortQuery(params, state.sorters);
        applyFilterQuery(params, state.filters);
      });
    });

    table.on('dataFiltered', function () {
      if (!readyForNavigation || applyingResponse) {
        return;
      }

      var state = readTableState(table, currentSorters);
      requestTableRefresh(function (params) {
        applySortQuery(params, state.sorters);
        applyFilterQuery(params, state.filters);
      });
    });

    window.addEventListener('popstate', function () {
      if (applyingResponse) {
        return;
      }

      fetchData(new URLSearchParams(window.location.search), {
        replaceHistory: false,
        resetOffset: false
      });
    });

    window.setTimeout(function () {
      readyForNavigation = true;
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
