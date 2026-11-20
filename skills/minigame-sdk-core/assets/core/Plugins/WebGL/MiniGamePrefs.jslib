mergeInto(LibraryManager.library, {
  MGKPrefsRead: function(fileNamePtr) {
    var text = '';
    try {
      var api = typeof tt !== 'undefined' ? tt : (typeof wx !== 'undefined' ? wx : null);
      var fileName = UTF8ToString(fileNamePtr);
      if (api && api.getFileSystemManager && api.env && api.env.USER_DATA_PATH && /^[A-Za-z0-9_.-]+$/.test(fileName)) {
        text = api.getFileSystemManager().readFileSync(api.env.USER_DATA_PATH + '/' + fileName, 'utf8') || '';
      }
    } catch (e) {}
    var size = lengthBytesUTF8(text) + 1;
    var result = _malloc(size);
    stringToUTF8(text, result, size);
    return result;
  },
  MGKPrefsWrite: function(fileNamePtr, jsonPtr) {
    try {
      var api = typeof tt !== 'undefined' ? tt : (typeof wx !== 'undefined' ? wx : null);
      var fileName = UTF8ToString(fileNamePtr);
      if (!api || !api.getFileSystemManager || !api.env || !api.env.USER_DATA_PATH || !/^[A-Za-z0-9_.-]+$/.test(fileName)) return 0;
      api.getFileSystemManager().writeFileSync(api.env.USER_DATA_PATH + '/' + fileName, UTF8ToString(jsonPtr), 'utf8');
      return 1;
    } catch (e) {
      return 0;
    }
  }
});
