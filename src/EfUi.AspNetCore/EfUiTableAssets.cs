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
    var fallback = document.querySelector('[data-role="efui-table-fallback"]');
    if (!(configElement instanceof HTMLScriptElement) || !(host instanceof HTMLElement)) {
      return;
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

    var columns = (config.columns || []).map(function (column) {
      return {
        title: column.title,
        field: column.field,
        formatter: function (cell) {
          var value = cell.getValue();
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

    new window.Tabulator(host, {
      data: config.rows || [],
      columns: columns,
      layout: 'fitColumns',
      reactiveData: false
    });

    container.classList.add('efui-table-enhancement-ready');
    if (fallback instanceof HTMLElement) {
      fallback.classList.add('efui-table-fallback-hidden');
    }
  });
});
""";

    internal const string StylesheetContent = """
.efui-table-enhancement {
  margin-bottom: 1rem;
}

.efui-table-host {
  min-height: 2rem;
}

.efui-table-fallback-hidden {
  display: none;
}
""";
}
