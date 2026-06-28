document.addEventListener('DOMContentLoaded', function() {
    const videoContainer = document.getElementById('video-player');
    if (!videoContainer) return;

    const videoUrl = videoContainer.dataset.videoUrl;
    if (!videoUrl) return;

    const video = document.createElement('video');
    video.controls = true;
    video.style.width = '100%';
    video.style.maxWidth = '800px';
    videoContainer.appendChild(video);

    if (Hls.isSupported()) {
        const hls = new Hls();
        hls.loadSource(videoUrl);
        hls.attachMedia(video);
        hls.on(Hls.Events.MANIFEST_PARSED, function() {
            console.log('HLS manifest loaded');
        });
    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
        video.src = videoUrl;
        video.addEventListener('loadedmetadata', function() {
            console.log('Native HLS supported');
        });
    }
});
