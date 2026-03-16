using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentTestingSystem.Data;
using StudentTestingSystem.Models.Entities;
using StudentTestingSystem.Models.ViewModels;

namespace StudentTestingSystem.Controllers;

[Authorize(Roles = "Teacher")]
public class TeacherController : Controller
{
    private readonly AppDbContext _context;

    public TeacherController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var courses = await _context.Courses
                .Include(c => c.Tests)
                .Include(c => c.CourseGroups)
                    .ThenInclude(cg => cg.Group)
                .ToListAsync();

        var viewModels = courses.Select(c => new CourseViewModel
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            TestsCount = c.Tests.Count,
            TotalMaxScore = c.Tests.Sum(t => t.MaxScore),
            SelectedGroupIds = c.CourseGroups.Select(cg => cg.GroupId).ToList(),
            IsHidden = c.IsHidden,
            IsLocked = c.IsLocked
        }).ToList();

        return View(viewModels);
    }

    #region Courses

    [HttpGet]
    public async Task<IActionResult> CreateCourse()
    {
        var model = new CourseViewModel
        {
            AllGroups = await _context.Groups
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name
                })
                .ToListAsync()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCourse(CourseViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AllGroups = await _context.Groups
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name
                })
                .ToListAsync();
            return View(model);
        }

        var course = new Course
        {
            Title = model.Title,
            Description = model.Description,
            CreatedAt = DateTime.Now,
            IsHidden = model.IsHidden,
            IsLocked = model.IsLocked
        };

        if (model.SelectedGroupIds != null && model.SelectedGroupIds.Any())
        {
            foreach (var gid in model.SelectedGroupIds.Distinct())
            {
                course.CourseGroups.Add(new CourseGroup
                {
                    GroupId = gid
                });
            }
        }

        _context.Courses.Add(course);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Курс успешно создан";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> EditCourse(int id)
    {
        var course = await _context.Courses
            .Include(c => c.CourseGroups)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null)
            return NotFound();

        var model = new CourseViewModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            IsHidden = course.IsHidden,
            IsLocked = course.IsLocked,
            SelectedGroupIds = course.CourseGroups.Select(cg => cg.GroupId).ToList(),
            AllGroups = await _context.Groups
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name
                })
                .ToListAsync()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> EditCourse(CourseViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AllGroups = await _context.Groups
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name
                })
                .ToListAsync();
            return View(model);
        }

        var course = await _context.Courses
            .Include(c => c.CourseGroups)
            .FirstOrDefaultAsync(c => c.Id == model.Id);

        if (course == null)
            return NotFound();

        course.Title = model.Title;
        course.Description = model.Description;
        course.IsHidden = model.IsHidden;
        course.IsLocked = model.IsLocked;

        course.CourseGroups.Clear();

        if (model.SelectedGroupIds != null && model.SelectedGroupIds.Any())
        {
            foreach (var gid in model.SelectedGroupIds.Distinct())
            {
                course.CourseGroups.Add(new CourseGroup
                {
                    CourseId = course.Id,
                    GroupId = gid
                });
            }
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Курс успешно обновлён";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        var course = await _context.Courses
            .Include(c => c.Tests)
            .ThenInclude(t => t.TestElements)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null)
            return NotFound();

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Курс удалён";
        return RedirectToAction("Index");
    }

    #endregion

    #region Tests

    [HttpGet]
    public async Task<IActionResult> CourseTests(int id)
    {
        var course = await _context.Courses
            .Include(c => c.Tests)
                .ThenInclude(t => t.TestElements)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null)
            return NotFound();

        ViewBag.CourseName = course.Title;
        ViewBag.CourseId = course.Id;

        return View(course.Tests.ToList());
    }

    [HttpGet]
    public async Task<IActionResult> CreateTest(int courseId)
    {
        var course = await _context.Courses.FindAsync(courseId);
        if (course == null)
            return NotFound();

        var model = new TestViewModel
        {
            CourseId = courseId,
            CourseName = course.Title,
            MaxAttempts = 3,
            MaxScore = 10,
            ShowCorrectAnswers = true,
            ShowResultImmediately = true,
            IsLecture = false, // по умолчанию обычный тест
            Elements = new List<TestElementViewModel>()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTest(TestViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var test = new Test
        {
            Title = model.Title,
            Description = model.Description,
            MaxScore = model.MaxScore,
            MaxAttempts = model.MaxAttempts,
            TimeLimitMinutes = model.TimeLimitMinutes,
            ShuffleQuestions = model.ShuffleQuestions,
            ShuffleAnswers = model.ShuffleAnswers,
            OneQuestionPerPage = model.OneQuestionPerPage,
            PreventBackNavigation = model.PreventBackNavigation,
            PassingScorePercent = model.PassingScorePercent,
            ShowCorrectAnswers = model.ShowCorrectAnswers,
            ShowResultImmediately = model.ShowResultImmediately,
            AvailableFrom = model.AvailableFrom,
            AvailableUntil = model.AvailableUntil,
            AccessPassword = model.AccessPassword,
            DetectTabSwitch = model.DetectTabSwitch,
            Instructions = model.Instructions,
            CourseId = model.CourseId,
            CreatedAt = DateTime.Now,
            DisableCopyPaste = model.DisableCopyPaste,
            IsLecture = model.IsLecture
        };

        //Обнуление баллов для лекции
        if (test.IsLecture)
        {
            test.MaxScore = 0;
        }
        _context.Tests.Add(test);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Тест создан. Добавьте вопросы.";
        return RedirectToAction("EditTest", new { id = test.Id });
    }

    [HttpGet]
    public async Task<IActionResult> EditTest(int id)
    {
        var test = await _context.Tests
            .Include(t => t.TestElements.OrderBy(e => e.Order))
            .Include(t => t.Course)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (test == null)
            return NotFound();

        var model = new TestViewModel
        {
            Id = test.Id,
            Title = test.Title,
            Description = test.Description,
            MaxScore = test.MaxScore,
            MaxAttempts = test.MaxAttempts,
            TimeLimitMinutes = test.TimeLimitMinutes,
            ShuffleQuestions = test.ShuffleQuestions,
            ShuffleAnswers = test.ShuffleAnswers,
            OneQuestionPerPage = test.OneQuestionPerPage,
            PreventBackNavigation = test.PreventBackNavigation,
            PassingScorePercent = test.PassingScorePercent,
            ShowCorrectAnswers = test.ShowCorrectAnswers,
            ShowResultImmediately = test.ShowResultImmediately,
            AvailableFrom = test.AvailableFrom,
            AvailableUntil = test.AvailableUntil,
            AccessPassword = test.AccessPassword,
            DetectTabSwitch = test.DetectTabSwitch,
            DisableCopyPaste = test.DisableCopyPaste,
            Instructions = test.Instructions,
            CourseId = test.CourseId,
            CourseName = test.Course?.Title,
            IsLecture = test.IsLecture,
            Elements = test.TestElements.Select(e => new TestElementViewModel
            {
                Id = e.Id,
                Type = e.Type,
                QuestionText = e.QuestionText,
                CorrectAnswer = e.CorrectAnswer,
                Options = e.OptionsJson,
                Weight = e.Weight,
                Hint = e.Hint,
                Explanation = e.Explanation,
                TimeLimit = e.TimeLimit,
                Tolerance = e.Tolerance,
                PenaltyPercent = e.PenaltyPercent,
                AllowPartialCredit = e.AllowPartialCredit,
                ImageUrl = e.ImageUrl,
                Order = e.Order
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateLecture(int courseId)
    {
        var course = await _context.Courses.FindAsync(courseId);
        if (course == null)
            return NotFound();

        var model = new LectureCreateViewModel
        {
            CourseId = courseId,
            CourseName = course.Title
        };

        return View(model); // отдельный LectureCreate.cshtml
    }

    [HttpPost]
    public async Task<IActionResult> CreateLecture(LectureCreateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var test = new Test
        {
            CourseId = model.CourseId,
            Title = model.Title,
            Description = model.Description,
            Instructions = model.Content,   // сюда кладём текст лекции
            IsLecture = true,
            MaxScore = 0,
            MaxAttempts = 1,
            ShowCorrectAnswers = false,
            ShowResultImmediately = false,
            CreatedAt = DateTime.Now
        };

        _context.Tests.Add(test);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Лекция создана";
        return RedirectToAction("CourseTests", new { id = model.CourseId });
    }
    [HttpGet]
    public async Task<IActionResult> EditLecture(int id)
    {
        var test = await _context.Tests
            .Include(t => t.Course)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsLecture);

        if (test == null)
            return NotFound();

        var model = new LectureCreateViewModel
        {
            CourseId = test.CourseId,
            CourseName = test.Course?.Title,
            Title = test.Title,
            Description = test.Description,
            Content = test.Instructions ?? test.Description ?? "",
        };

        ViewBag.TestId = test.Id;
        return View("EditLecture", model);
    }

    [HttpPost]
    public async Task<IActionResult> EditLecture(int id, LectureCreateViewModel model)
    {
        if (!ModelState.IsValid)
            return View("EditLecture", model);

        var test = await _context.Tests.FirstOrDefaultAsync(t => t.Id == id && t.IsLecture);
        if (test == null)
            return NotFound();

        test.Title = model.Title;
        test.Description = model.Description;
        test.Instructions = model.Content;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Лекция обновлена";
        return RedirectToAction("CourseTests", new { id = model.CourseId });
    }

    [HttpPost]
    public async Task<IActionResult> AddElement(int testId, TestElementViewModel element)
    {
        var test = await _context.Tests
            .Include(t => t.TestElements)
            .FirstOrDefaultAsync(t => t.Id == testId);

        if (test == null)
            return NotFound();

        var maxOrder = test.TestElements.Any() ? test.TestElements.Max(e => e.Order) : 0;

        var newElement = new TestElement
        {
            TestId = testId,
            Type = element.Type,
            QuestionText = element.QuestionText,
            CorrectAnswer = element.CorrectAnswer,
            OptionsJson = element.Options,
            Weight = element.Weight,
            Hint = element.Hint,
            Explanation = element.Explanation,
            TimeLimit = element.TimeLimit,
            Tolerance = element.Tolerance,
            PenaltyPercent = element.PenaltyPercent,
            AllowPartialCredit = element.AllowPartialCredit,
            ImageUrl = element.ImageUrl,
            Order = maxOrder + 1
        };

        // Автогенерация правильного ответа для Matching
        if (newElement.Type == ElementType.Matching)
        {
            // OptionsJson в формате "Левый:Правый|Левый2:Правый2|..."
            var rawPairs = !string.IsNullOrWhiteSpace(newElement.OptionsJson)
                ? newElement.OptionsJson.Split('|', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();

            var count = rawPairs.Length;

            if (count > 0)
            {
                // "1=1|2=2|3=3"
                var parts = Enumerable.Range(1, count)
                    .Select(i => $"{i}={i}");
                newElement.CorrectAnswer = string.Join('|', parts);
            }
            else
            {
                newElement.CorrectAnswer = string.Empty;
            }
        }

        _context.TestElements.Add(newElement);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Вопрос добавлен";
        return RedirectToAction("EditTest", new { id = testId });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateElement(TestElementViewModel element)
    {
        var existingElement = await _context.TestElements.FindAsync(element.Id);
        if (existingElement == null)
            return NotFound();

        existingElement.Type = element.Type;
        existingElement.QuestionText = element.QuestionText;
        existingElement.CorrectAnswer = element.CorrectAnswer;
        existingElement.OptionsJson = element.Options;
        existingElement.Weight = element.Weight;
        existingElement.Hint = element.Hint;
        existingElement.Explanation = element.Explanation;
        existingElement.TimeLimit = element.TimeLimit;
        existingElement.Tolerance = element.Tolerance;
        existingElement.PenaltyPercent = element.PenaltyPercent;
        existingElement.AllowPartialCredit = element.AllowPartialCredit;
        existingElement.ImageUrl = element.ImageUrl;

        // Автогенерация правильного ответа для Matching при изменении вопроса
        if (existingElement.Type == ElementType.Matching)
        {
            var rawPairs = !string.IsNullOrWhiteSpace(existingElement.OptionsJson)
                ? existingElement.OptionsJson.Split('|', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();

            var count = rawPairs.Length;

            if (count > 0)
            {
                var parts = Enumerable.Range(1, count)
                    .Select(i => $"{i}={i}");
                existingElement.CorrectAnswer = string.Join('|', parts);
            }
            else
            {
                existingElement.CorrectAnswer = string.Empty;
            }
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Вопрос обновлён";
        return RedirectToAction("EditTest", new { id = existingElement.TestId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteElement(int id)
    {
        var element = await _context.TestElements.FindAsync(id);
        if (element == null)
            return NotFound();

        var testId = element.TestId;
        _context.TestElements.Remove(element);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Вопрос удалён";
        return RedirectToAction("EditTest", new { id = testId });
    }

    [HttpPost]
    public async Task<IActionResult> DuplicateTest(int id)
    {
        var test = await _context.Tests
            .Include(t => t.TestElements)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (test == null)
            return NotFound();

        var newTest = new Test
        {
            Title = test.Title + " (копия)",
            Description = test.Description,
            MaxScore = test.MaxScore,
            MaxAttempts = test.MaxAttempts,
            TimeLimitMinutes = test.TimeLimitMinutes,
            ShuffleQuestions = test.ShuffleQuestions,
            ShuffleAnswers = test.ShuffleAnswers,
            OneQuestionPerPage = test.OneQuestionPerPage,
            PreventBackNavigation = test.PreventBackNavigation,
            PassingScorePercent = test.PassingScorePercent,
            ShowCorrectAnswers = test.ShowCorrectAnswers,
            ShowResultImmediately = test.ShowResultImmediately,
            DetectTabSwitch = test.DetectTabSwitch,
            Instructions = test.Instructions,
            CourseId = test.CourseId,
            CreatedAt = DateTime.Now,
            IsLecture = test.IsLecture
        };

        _context.Tests.Add(newTest);
        await _context.SaveChangesAsync();

        foreach (var element in test.TestElements)
        {
            var newElement = new TestElement
            {
                TestId = newTest.Id,
                Type = element.Type,
                QuestionText = element.QuestionText,
                CorrectAnswer = element.CorrectAnswer,
                OptionsJson = element.OptionsJson,
                Weight = element.Weight,
                Hint = element.Hint,
                Explanation = element.Explanation,
                TimeLimit = element.TimeLimit,
                Tolerance = element.Tolerance,
                PenaltyPercent = element.PenaltyPercent,
                AllowPartialCredit = element.AllowPartialCredit,
                ImageUrl = element.ImageUrl,
                Order = element.Order
            };
            _context.TestElements.Add(newElement);
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Тест скопирован";
        return RedirectToAction("EditTest", new { id = newTest.Id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTest(int id)
    {
        var test = await _context.Tests
            .Include(t => t.TestElements)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (test == null)
            return NotFound();

        var courseId = test.CourseId;
        _context.Tests.Remove(test);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Тест удалён";
        return RedirectToAction("CourseTests", new { id = courseId });
    }

    #endregion

    #region Statistics

    [HttpGet]
    public async Task<IActionResult> Statistics()
    {
        var tests = await _context.Tests
            .Include(t => t.Course)
            .Include(t => t.TestElements)
            .Include(t => t.StudentResults)
                .ThenInclude(r => r.Attempts)
            .ToListAsync();

        var stats = tests.Select(t => new TestStatisticsViewModel
        {
            TestId = t.Id,
            TestTitle = t.Title,
            TotalAttempts = t.StudentResults.Sum(r => r.AttemptsUsed),
            UniqueStudents = t.StudentResults.Count,
            AverageScore = t.StudentResults.Any() ? t.StudentResults.Average(r => r.BestScore) : 0,
            HighestScore = t.StudentResults.Any() ? t.StudentResults.Max(r => r.BestScore) : 0,
            LowestScore = t.StudentResults.Any() ? t.StudentResults.Min(r => r.BestScore) : 0,
            PassedCount = t.StudentResults.Count(r => r.IsPassed),
            FailedCount = t.StudentResults.Count(r => !r.IsPassed && r.IsCompleted),
            PassRate = t.StudentResults.Any(r => r.IsCompleted) 
                ? (double)t.StudentResults.Count(r => r.IsPassed) / t.StudentResults.Count(r => r.IsCompleted) * 100 
                : 0,
            AverageTimeMinutes = t.StudentResults.Any() 
                ? t.StudentResults.Average(r => r.TotalTimeSeconds / 60.0) 
                : 0
        }).ToList();

        return View(stats);
    }

    [HttpGet]
    public async Task<IActionResult> TestStatistics(int id)
    {
        var test = await _context.Tests
            .Include(t => t.TestElements)
            .Include(t => t.StudentResults)
                .ThenInclude(r => r.User)
            .Include(t => t.StudentResults)
                .ThenInclude(r => r.Attempts)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (test == null)
            return NotFound();

        // при желании можно сразу отсортировать попытки и т.п.
        foreach (var result in test.StudentResults)
        {
            result.Attempts = result.Attempts
                .OrderBy(a => a.AttemptNumber)
                .ToList();
        }

        ViewBag.Test = test;
        return View(test.StudentResults.ToList());
    }

    #endregion

    #region Password Reset Requests

    [HttpGet]
    public async Task<IActionResult> PasswordRequests()
    {
        var requests = await _context.PasswordResetRequests
            .Include(r => r.User)
            .Where(r => !r.IsProcessed)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        return View(requests);
    }

    [HttpPost]
    public async Task<IActionResult> ApprovePasswordReset(int id, string newPassword)
    {
        var request = await _context.PasswordResetRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null || request.User == null)
            return NotFound();

        request.User.PasswordHash = BCryptHelper.HashPassword(newPassword);
        request.User.IsBlocked = false;
        request.IsProcessed = true;
        request.ProcessedAt = DateTime.Now;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Пароль для {request.User.FullName} изменён";
        return RedirectToAction("PasswordRequests");
    }

    [HttpPost]
    public async Task<IActionResult> RejectPasswordReset(int id)
    {
        var request = await _context.PasswordResetRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null || request.User == null)
            return NotFound();

        request.User.IsBlocked = false;
        request.IsProcessed = true;
        request.ProcessedAt = DateTime.Now;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Запрос отклонён, аккаунт разблокирован";
        return RedirectToAction("PasswordRequests");
    }

    #endregion

    #region Student Results Management

    /// <summary>
    /// Удаление конкретной попытки студента
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteAttempt(int attemptId)
    {
        var attempt = await _context.TestAttempts
            .Include(a => a.StudentResult)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        if (attempt == null)
            return NotFound();

        var result = attempt.StudentResult;
        if (result == null)
            return NotFound();

        var testId = result.TestId;

        // Удаляем попытку
        _context.TestAttempts.Remove(attempt);
        result.AttemptsUsed--;

        // Пересчитываем лучший результат
        var remainingAttempts = await _context.TestAttempts
            .Where(a => a.StudentResultId == result.Id && a.Id != attemptId)
            .ToListAsync();

        if (remainingAttempts.Any())
        {
            result.BestScore = remainingAttempts.Max(a => a.Score);
            result.LastScore = remainingAttempts.OrderByDescending(a => a.CompletedAt).First().Score;
            result.TotalTimeSeconds = remainingAttempts.Sum(a => a.TimeSpentSeconds);
        }
        else
        {
            // Если попыток не осталось, удаляем весь результат
            _context.StudentResults.Remove(result);
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Попытка удалена";
        return RedirectToAction("TestStatistics", new { id = testId });
    }

    /// <summary>
    /// Сброс всех попыток студента по тесту
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ResetStudentResult(int resultId)
    {
        var result = await _context.StudentResults
            .Include(r => r.Attempts)
            .FirstOrDefaultAsync(r => r.Id == resultId);

        if (result == null)
            return NotFound();

        var testId = result.TestId;

        _context.TestAttempts.RemoveRange(result.Attempts);
        _context.StudentResults.Remove(result);

        await _context.SaveChangesAsync();

        TempData["Success"] = "Все попытки студента удалены";
        return RedirectToAction("TestStatistics", new { id = testId });
    }

    /// <summary>
    /// Аннулировать (отменить) конкретную попытку студента
    /// Попытка остаётся в истории, но не учитывается в результатах
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> VoidAttempt(int attemptId, string? reason)
    {
        var attempt = await _context.TestAttempts
            .Include(a => a.StudentResult)
            .ThenInclude(r => r!.Attempts)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        if (attempt == null)
            return NotFound();

        var result = attempt.StudentResult;
        if (result == null)
            return NotFound();

        var testId = result.TestId;

        // Помечаем попытку как аннулированную
        attempt.IsVoided = true;
        attempt.VoidReason = reason ?? "Отменено преподавателем";

        // Уменьшаем счётчик использованных попыток (даём ещё одну попытку)
        result.AttemptsUsed = Math.Max(0, result.AttemptsUsed - 1);

        // Пересчитываем лучший и последний результат (без учёта аннулированных)
        var validAttempts = (result.Attempts ?? Enumerable.Empty<TestAttempt>())
            .Where(a => !a.IsVoided && a.Id != attemptId)
            .ToList();

        if (validAttempts.Any())
        {
            result.BestScore = validAttempts.Max(a => a.Score);
            result.LastScore = validAttempts.OrderByDescending(a => a.CompletedAt).First().Score;
            result.IsPassed = validAttempts.Any(a => a.Score >= (result.Test?.MaxScore ?? 0) * (result.Test?.PassingScorePercent ?? 0) / 100.0);
        }
        else
        {
            result.BestScore = 0;
            result.LastScore = 0;
            result.IsPassed = false;
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Попытка #{attempt.AttemptNumber} аннулирована. Студенту добавлена 1 попытка.";
        return RedirectToAction("TestStatistics", new { id = testId });
    }

    /// <summary>
    /// Отменить аннулирование попытки (восстановить)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RestoreAttempt(int attemptId)
    {
        var attempt = await _context.TestAttempts
            .Include(a => a.StudentResult)
            .ThenInclude(r => r!.Attempts)
            .Include(a => a.StudentResult!.Test)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        if (attempt == null || !attempt.IsVoided)
            return NotFound();

        var result = attempt.StudentResult;
        if (result == null)
            return NotFound();

        var testId = result.TestId;

        // Восстанавливаем попытку
        attempt.IsVoided = false;
        attempt.VoidReason = null;

        // Увеличиваем счётчик использованных попыток
        result.AttemptsUsed++;

        // Пересчитываем результаты
        var validAttempts = (result.Attempts ?? Enumerable.Empty<TestAttempt>()).Where(a => !a.IsVoided).ToList();
        if (validAttempts.Any())
        {
            result.BestScore = validAttempts.Max(a => a.Score);
            result.LastScore = validAttempts.OrderByDescending(a => a.CompletedAt).First().Score;
            
            var test = result.Test;
            var passingScore = (test?.MaxScore ?? 0) * (test?.PassingScorePercent ?? 0) / 100.0;
            result.IsPassed = validAttempts.Any(a => a.Score >= passingScore);
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Попытка #{attempt.AttemptNumber} восстановлена.";
        return RedirectToAction("TestStatistics", new { id = testId });
    }

    /// <summary>
    /// Добавление дополнительных попыток студенту
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddExtraAttempts(int resultId, int extraAttempts)
    {
        var result = await _context.StudentResults
            .Include(r => r.Test)
            .FirstOrDefaultAsync(r => r.Id == resultId);

        if (result == null)
            return NotFound();

        // Добавляем дополнительные попытки (уменьшаем счётчик использованных)
        result.AttemptsUsed = Math.Max(0, result.AttemptsUsed - extraAttempts);

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Добавлено {extraAttempts} дополнительных попыток";
        return RedirectToAction("TestStatistics", new { id = result.TestId });
    }

    /// <summary>
    /// Просмотр детальной информации о попытках студента
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> StudentResultDetails(int resultId)
    {
        var result = await _context.StudentResults
            .Include(r => r.User)
            .Include(r => r.Test)
            .Include(r => r.Attempts.OrderByDescending(a => a.AttemptNumber))
            .FirstOrDefaultAsync(r => r.Id == resultId);

        if (result == null)
            return NotFound();

        return View(result);
    }

    #endregion
    #region Groups
    [HttpGet]
    [HttpGet]
    public async Task<IActionResult> Groups()
    {
        var groups = await _context.Groups
            .Include(g => g.Students)
            .OrderBy(g => g.Name)
            .ToListAsync();

        return View(groups);
    }

    [HttpGet]
    public IActionResult CreateGroup()
    {
        return View(new Group());
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup(Group model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var group = new Group
        {
            Name = model.Name
        };

        _context.Groups.Add(group);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Группа создана";
        return RedirectToAction("Groups");
    }

    [HttpGet]
    public async Task<IActionResult> EditGroup(int id)
    {
        var group = await _context.Groups.FindAsync(id);
        if (group == null)
            return NotFound();

        var model = new Group
        {
            Id = group.Id,
            Name = group.Name
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> EditGroup(Group model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var group = await _context.Groups.FindAsync(model.Id);
        if (group == null)
            return NotFound();

        group.Name = model.Name;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Название группы обновлено";
        return RedirectToAction("Groups");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        var group = await _context.Groups
            .Include(g => g.Students)
            .Include(g => g.CourseGroups)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null)
            return NotFound();

        // Удаляем все связи курс–группа
        _context.CourseGroups.RemoveRange(group.CourseGroups);

        // Удаляем всех студентов этой группы
        _context.Users.RemoveRange(group.Students);

        // Удаляем саму группу
        _context.Groups.Remove(group);

        await _context.SaveChangesAsync();

        TempData["Success"] = "Группа и все её студенты удалены";
        return RedirectToAction("Groups");
    }

    #endregion
}
