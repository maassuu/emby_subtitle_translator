define(['baseView', 'loading', 'emby-input', 'emby-button', 'emby-select', 'emby-checkbox'], function (BaseView, loading) {
    'use strict';

    var pluginId = 'afd87012-b805-487e-b115-93c754fac738';
    var textFields = ['targetLanguage', 'provider', 'geminiModel', 'anthropicModel', 'openAiBaseUrl', 'openAiModel'];
    var secretFields = ['geminiApiKey', 'anthropicApiKey', 'openAiApiKey'];
    var numberFields = ['requestsPerMinute'];
    var checkboxFields = ['overwriteExisting'];

    function configKey(id) {
        return id.charAt(0).toUpperCase() + id.slice(1);
    }

    function showProviderSection(view) {
        var provider = view.querySelector('#provider').value;
        view.querySelectorAll('.providerSection').forEach(function (section) {
            section.classList.toggle('hide', section.getAttribute('data-provider') !== provider);
        });
    }

    function load(view) {
        loading.show();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            textFields.concat(numberFields).forEach(function (id) {
                view.querySelector('#' + id).value = config[configKey(id)];
            });
            checkboxFields.forEach(function (id) {
                view.querySelector('#' + id).checked = !!config[configKey(id)];
            });
            secretFields.forEach(function (id) {
                var input = view.querySelector('#' + id);
                input.value = '';
                input.setAttribute('placeholder', config[configKey(id)] ? 'zapisany - wpisz nowy, aby zmienić' : 'nie ustawiono');
            });
            showProviderSection(view);
            loading.hide();
        });
    }

    function save(view) {
        loading.show();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            textFields.forEach(function (id) {
                config[configKey(id)] = view.querySelector('#' + id).value.trim();
            });
            numberFields.forEach(function (id) {
                config[configKey(id)] = parseInt(view.querySelector('#' + id).value, 10) || 0;
            });
            checkboxFields.forEach(function (id) {
                config[configKey(id)] = view.querySelector('#' + id).checked;
            });
            secretFields.forEach(function (id) {
                var value = view.querySelector('#' + id).value.trim();
                if (value) {
                    config[configKey(id)] = value;
                }
            });
            ApiClient.updatePluginConfiguration(pluginId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
                load(view);
            });
        });
    }

    function View(view, params) {
        BaseView.apply(this, arguments);
        view.querySelector('#provider').addEventListener('change', function () {
            showProviderSection(view);
        });
        view.querySelector('form').addEventListener('submit', function (e) {
            e.preventDefault();
            save(view);
            return false;
        });
    }

    Object.assign(View.prototype, BaseView.prototype);

    View.prototype.onResume = function (options) {
        BaseView.prototype.onResume.apply(this, arguments);
        load(this.view);
    };

    return View;
});
