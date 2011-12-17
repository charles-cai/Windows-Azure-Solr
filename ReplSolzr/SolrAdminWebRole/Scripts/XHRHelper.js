
function XHRHelper() { }

XHRHelper.prototype.get = function (options) {
    var xhr = this.initXhr();
    xhr.onreadystatechange = function () {
        if (xhr.readyState != 4) {
            return;
        }
        if (xhr.status == 200) {
            options.successCallback(xhr.responseText, xhr.status, xhr);
        }
        else {
            options.errorCallback(xhr, xhr.status, null);
        }
    }
    xhr.open("GET", options.url, true);
    xhr.send();
}

XHRHelper.prototype.post = function (options) {
    var xhr = this.initXhr();
    xhr.onreadystatechange = function () {
        if (xhr.readyState != 4) {
            return;
        }
        if (xhr.status == 200) {
            options.successCallback(xhr.responseText, xhr.status, xhr);
        }
        else {
            options.errorCallback(xhr, xhr.status, null);
        }
    }
    xhr.open("POST", options.url, true);
    xhr.send(options.data);
}

XHRHelper.prototype.initXhr = function () {
    if (window.XMLHttpRequest) {
        return new window.XMLHttpRequest();
    }
    return new ActiveXObject("Microsoft.XMLHTTP");
}