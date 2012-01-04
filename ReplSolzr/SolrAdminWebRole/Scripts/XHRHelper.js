/*
* Copyright 2011 Microsoft Corporation
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*   http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

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