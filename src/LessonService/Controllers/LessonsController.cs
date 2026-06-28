using LessonService.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.DTO;

namespace LessonService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LessonsController(ILessonService lessonService, ILogger<LessonsController> logger) : ControllerBase
{
    private readonly ILessonService _lessonService = lessonService;
    private readonly ILogger<LessonsController> _logger = logger;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LessonDto>>> GetAllLessons()
    {
        var lessons = await _lessonService.GetAllLessonsAsync();
        return Ok(lessons);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LessonDto>> GetLesson(Guid id)
    {
        var lesson = await _lessonService.GetLessonByIdAsync(id);
        if (lesson == null)
        {
            return NotFound();
        }
        return Ok(lesson);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LessonDto>> CreateLesson([FromBody] CreateLessonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(new { error = "Title is required" });
        }

        var lesson = await _lessonService.CreateLessonAsync(dto);
        _logger.LogInformation("Created lesson {Id}", lesson.Id);
        
        return CreatedAtAction(nameof(GetLesson), new { id = lesson.Id }, lesson);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LessonDto>> UpdateLesson(Guid id, [FromBody] UpdateLessonDto dto)
    {
        var lesson = await _lessonService.UpdateLessonAsync(id, dto);
        if (lesson == null)
        {
            return NotFound();
        }
        return Ok(lesson);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLesson(Guid id)
    {
        var deleted = await _lessonService.DeleteLessonAsync(id);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPut("{id}/video-status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateVideoStatus(Guid id, [FromBody] VideoStatus status)
    {
        await _lessonService.UpdateVideoStatusAsync(id, status);
        return NoContent();
    }
}
