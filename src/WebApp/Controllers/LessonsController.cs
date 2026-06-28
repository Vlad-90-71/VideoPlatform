using Microsoft.AspNetCore.Mvc;
using WebApp.Services;

namespace WebApp.Controllers;

public class LessonsController(ILessonServiceClient lessonService, ILogger<LessonsController> logger) : Controller
{
    private readonly ILessonServiceClient _lessonService = lessonService;
    private readonly ILogger<LessonsController> _logger = logger;

    public async Task<IActionResult> Index()
    {
        var lessons = await _lessonService.GetAllLessonsAsync();
        return View(lessons);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var lesson = await _lessonService.GetLessonByIdAsync(id);
        if (lesson == null)
        {
            return NotFound();
        }
        return View(lesson);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, string description)
    {
        var lesson = await _lessonService.CreateLessonAsync(title, description);
        return RedirectToAction(nameof(Details), new { id = lesson.Id });
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var lesson = await _lessonService.GetLessonByIdAsync(id);
        if (lesson == null)
        {
            return NotFound();
        }
        return View(lesson);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, string title, string description)
    {
        await _lessonService.UpdateLessonAsync(id, title, description, null);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _lessonService.DeleteLessonAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
