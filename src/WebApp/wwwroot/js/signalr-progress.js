document.addEventListener('DOMContentLoaded', function() {
    const progressDisplay = document.getElementById('progress-display');
    if (!progressDisplay) return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/videoProgressHub')
        .build();

    connection.start().then(function() {
        console.log('SignalR connected');
        
        const lessonId = window.location.pathname.split('/').pop();
        connection.invoke('JoinVideoGroup', lessonId).catch(function(err) {
            console.error(err);
        });
    }).catch(function(err) {
        console.error('SignalR connection error:', err);
    });

    connection.on('VideoProgress', function(progressEvent) {
        console.log('Progress:', progressEvent);
        progressDisplay.textContent = Прогресс: %;
        
        if (progressEvent.status === 2) { // Completed
            progressDisplay.textContent = 'Видео готово!';
            setTimeout(() => location.reload(), 2000);
        } else if (progressEvent.status === 3) { // Failed
            progressDisplay.textContent = 'Ошибка обработки видео';
            progressDisplay.className = 'text-danger';
        }
    });
});
