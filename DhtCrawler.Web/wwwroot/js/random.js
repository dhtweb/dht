$(document).ready(function () {
    var images = ['/images/bg1.jpg', '/images/bg2.jpg'];
    var date = new Date();
    var bgIndex = date.getHours() > 12 ? 1 : 0;
    $('.new-home').css({ 'background-image': 'url(' + images[bgIndex] + ')' });///images/
    //bg1.jpg
});