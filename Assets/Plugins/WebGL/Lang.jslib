mergeInto(LibraryManager.library,
{
	// Возвращает код языка, полученный из environment.i18n.lang
	// при инициализации SDK Яндекс Игр (см. index.html шаблона).
	// Переменная ysdkLang заполняется до старта Unity.
	GetSdkLang_js: function ()
	{
		var lang = (typeof ysdkLang !== 'undefined' && ysdkLang) ? ysdkLang : '';

		if (!lang && typeof navigator !== 'undefined')
			lang = navigator.language || '';

		var bufferSize = lengthBytesUTF8(lang) + 1;
		var buffer = _malloc(bufferSize);
		stringToUTF8(lang, buffer, bufferSize);

		return buffer;
	}
});
