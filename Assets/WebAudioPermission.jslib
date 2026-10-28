mergeInto(LibraryManager.library, {
  WebAudio_InstallGestureResume__deps: ['$WEBAudio'],
  WebAudio_InstallGestureResume: function () {
    if (Module['gameAudioGestureResume']) return;
    var resume = function () {
      var context = WEBAudio.audioContext;
      if (context && context.state !== 'running' && context.state !== 'closed') {
        context.resume().then(function () {
          console.log('[24tu Audio] Browser audio resumed: ' + context.state);
        }).catch(function () { /* A later user gesture can retry. */ });
      }
    };
    Module['gameAudioGestureResume'] = resume;
    // Direct trusted events, including keyboard-only desktop play and the
    // mobile start/resume button. Do not create a separate AudioContext.
    ['pointerdown', 'pointerup', 'touchend', 'keydown'].forEach(function (type) {
      window.addEventListener(type, resume, {capture:true, passive:true});
    });
    Module.deinitializers.push(function () {
      ['pointerdown', 'pointerup', 'touchend', 'keydown'].forEach(function (type) {
        window.removeEventListener(type, resume, true);
      });
      delete Module['gameAudioGestureResume'];
    });
  },
  WebAudio_IsRunning__deps: ['$WEBAudio'],
  WebAudio_IsRunning: function () {
    return WEBAudio.audioContext && WEBAudio.audioContext.state === 'running' ? 1 : 0;
  }
});
