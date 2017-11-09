$(document).ready(function () {
    var images = ['bg1.jpg', 'bg2.jpg'];
    var date = new Date();
    var bgIndex = date.getHours() > 12 ? 1 : 0;
    $('.new-home').css({ 'background-image': 'url(/images/' + images[bgIndex] + ')' });
    //bg1.jpg
});