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
      host.classList.toggle('efui-table-host-loading', isLoading);
      if (!(loading instanceof HTMLElement)) {
        return;
      }

      loading.hidden = !isLoading;
      if (message) {
        loading.textContent = message;
      }
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

    function navigate(listUrl, mutator) {
      var params = new URLSearchParams(window.location.search);
      mutator(params);
      params.set('offset', '0');
      setLoading(true, 'Loading table…');
      var query = params.toString();
      window.location.assign(listUrl + (query ? '?' + query : ''));
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
    var columnMap = {};
    (config.columns || []).forEach(function (column) {
      if (column && column.field) {
        columnMap[column.field] = column;
      }
    });

    function readInitialSort(sorts) {
      return (sorts || [])
        .map(function (sort) {
          var field = sort.field || sort.Field || '';
          var direction = (sort.direction || sort.Direction || '').toLowerCase();
          if (!field || !direction) {
            return null;
          }

          return { column: field, dir: direction };
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

    function getFilterOperator(field) {
      var column = columnMap[field] || {};
      return column.activeFilterOperator || column.filterOperator || 'contains';
    }

    function applyFilterQuery(params, filters) {
      clearFilterQuery(params);
      (filters || [])
        .filter(function (filter) {
          return !!filter && !!filter.field && !isBlankFilterValue(filter.value);
        })
        .forEach(function (filter, filterIndex) {
          params.set('filter.' + filterIndex + '.field', filter.field);
          params.set('filter.' + filterIndex + '.op', getFilterOperator(filter.field));
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

    var columns = (config.columns || []).map(function (column) {
      var isActionsColumn = column.field === '__actions';

      return {
        title: column.title || column.field || '',
        field: column.field,
        headerSort: column.headerSort !== false,
        headerSortTristate: column.headerSort !== false,
        headerFilter: column.headerFilter === false ? false : column.headerFilter,
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

    var filterNavigationHandle = 0;
    var readyForNavigation = false;

    setLoading(true, 'Loading table…');

    var table = new window.Tabulator(host, {
      data: config.rows || [],
      columns: columns,
      layout: 'fitColumns',
      reactiveData: false,
      initialSort: readInitialSort(config.query && config.query.sorts),
      initialHeaderFilter: readInitialHeaderFilter(config.columns),
      headerFilterLiveFilterDelay: 400,
      dataLoader: true,
      dataLoaderLoading: '<div class="efui-tabulator-loader">Loading table…</div>',
      dataLoaderError: '<div class="efui-tabulator-loader efui-tabulator-loader-error">Unable to load table.</div>'
    });

    table.on('dataSorting', function (sorters) {
      if (!readyForNavigation) {
        return;
      }

      navigate(listUrl, function (params) {
        applySortQuery(params, sorters);
      });
    });

    table.on('dataFiltering', function (filters) {
      if (!readyForNavigation) {
        return;
      }

      clearTimeout(filterNavigationHandle);
      filterNavigationHandle = setTimeout(function () {
        navigate(listUrl, function (params) {
          applyFilterQuery(params, filters);
        });
      }, 400);
    });

    window.setTimeout(function () {
      readyForNavigation = true;
      setLoading(false, '');
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

.efui-table-loading {
  margin-bottom: 0.75rem;
  padding: 0.75rem 0.875rem;
  border: 1px solid #bfdbfe;
  border-radius: 0.625rem;
  background: #eff6ff;
  color: #1d4ed8;
  font-weight: 600;
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
