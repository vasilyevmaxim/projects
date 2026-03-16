using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentTestingSystem.Data;
using StudentTestingSystem.Models.Entities;
using StudentTestingSystem.Models.ViewModels;
using StudentTestingSystem.Services;
using System.Security.Claims;
using System.Text.Json;

namespace StudentTestingSystem.Controllers;

[Authorize(Roles = "Student")]
public class StudentController : Controller
{
    private readonly AppDbContext _context;
    private readonly IGradingService _gradingService;

    public StudentController(AppDbContext context, IGradingService gradingService)
    {
        _context = context;
        _gradingService = gradingService;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var user = await _context.Users
            .Include(u => u.Group)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return Unauthorized(); // или RedirectToAction("Login", "Account");

        var userGroupId = user.GroupId; // локальная переменная, чтобы не таскать user в выражение

        var now = DateTime.Now;

        var courses = await _context.Courses
            .Where(c => !c.IsHidden) // скрытые курсы не показываем студентам
            .Include(c => c.Tests)
                .ThenInclude(t => t.TestElements)
            .Include(c => c.Tests)
                .ThenInclude(t => t.StudentResults.Where(sr => sr.UserId == userId))
            .Include(c => c.CourseGroups)
            .ToListAsync();

        var model = new StudentDashboardViewModel
        {
            StudentName = user.FullName,
            GroupNumber = user.Group?.Name ?? user.GroupNumber ?? "",
            Courses = courses.Select(c => new CourseResultViewModel
            {
                CourseId = c.Id,
                CourseTitle = c.Title,
                CourseDescription = c.Description,
                MaxScore = c.Tests.Sum(t => t.MaxScore),
                TotalScore = c.Tests.Sum(t => t.StudentResults.FirstOrDefault()?.BestScore ?? 0),
                IsLocked = c.IsLocked,

                Tests = c.Tests.Select(t =>
                {
                    var result = t.StudentResults.FirstOrDefault();
                    var isAvailableByDate = (!t.AvailableFrom.HasValue || t.AvailableFrom <= now) &&
                                            (!t.AvailableUntil.HasValue || t.AvailableUntil >= now);

                    // если курс заблокирован – тесты не доступны, но статистика остаётся
                    var isAvailable = !c.IsLocked && isAvailableByDate;
                    var canTake = isAvailable && (result?.AttemptsUsed ?? 0) < t.MaxAttempts;

                    return new TestResultViewModel
                    {
                        TestId = t.Id,
                        TestTitle = t.Title,
                        TestDescription = t.Description,
                        MaxScore = t.MaxScore,
                        BestScore = result?.BestScore ?? 0,
                        LastScore = result?.LastScore ?? 0,
                        AttemptsUsed = result?.AttemptsUsed ?? 0,
                        MaxAttempts = t.MaxAttempts,
                        IsCompleted = result?.IsCompleted ?? false,
                        IsPassed = result?.IsPassed ?? false,
                        CanTake = canTake,
                        TimeLimitMinutes = t.TimeLimitMinutes,
                        PassingScorePercent = t.PassingScorePercent,
                        AvailableFrom = t.AvailableFrom,
                        AvailableUntil = t.AvailableUntil,
                        IsAvailable = isAvailable,
                        RequiresPassword = !string.IsNullOrEmpty(t.AccessPassword),
                        QuestionsCount = t.TestElements.Count,
                        IsLecture = t.IsLecture
                    };
                }).ToList()
            }).ToList()
        };

        model.TotalScore = model.Courses.Sum(c => c.TotalScore);
        model.TotalMaxScore = model.Courses.Sum(c => c.MaxScore);
        model.CompletedTests = model.Courses.SelectMany(c => c.Tests).Count(t => t.IsCompleted);
        model.TotalTests = model.Courses.SelectMany(c => c.Tests).Count();
        model.PassedTests = model.Courses.SelectMany(c => c.Tests).Count(t => t.IsPassed);

        foreach (var course in model.Courses)
        {
            course.ProgressPercent = course.MaxScore > 0 
                ? Math.Round(course.TotalScore / course.MaxScore * 100, 1) 
                : 0;
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> StartTest(int id)
    {
        var userId = GetUserId();
        var test = await _context.Tests
            .Include(t => t.Course)
            .Include(t => t.TestElements)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (test == null)
            return NotFound();

        if (test.IsLecture)
        {
            var lectureModel = new LectureViewModel
            {
                TestId = test.Id,
                CourseId = test.CourseId,
                Title = test.Title,
                Content = test.Instructions ?? test.Description ?? ""
            };

            return View("Lecture", lectureModel);
        }

        var existingResult = await _context.StudentResults
            .FirstOrDefaultAsync(sr => sr.UserId == userId && sr.TestId == id);

        int attemptsUsed = existingResult?.AttemptsUsed ?? 0;
        int attemptsLeft = test.MaxAttempts - attemptsUsed;

        if (attemptsLeft <= 0)
        {
            TempData["Error"] = "Вы исчерпали все попытки для этого теста";
            return RedirectToAction("Index");
        }

        var model = new TestAccessViewModel
        {
            TestId = test.Id,
            TestTitle = test.Title,
            Instructions = test.Instructions,
            TimeLimitMinutes = test.TimeLimitMinutes,
            QuestionsCount = test.TestElements.Count,
            AttemptsLeft = attemptsLeft,
            RequiresPassword = !string.IsNullOrEmpty(test.AccessPassword)
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> StartTest(TestAccessViewModel model)
    {
        var test = await _context.Tests.FindAsync(model.TestId);
        if (test == null)
            return NotFound();

        if (!string.IsNullOrEmpty(test.AccessPassword) && 
            test.AccessPassword != model.EnteredPassword)
        {
            model.ErrorMessage = "Неверный пароль доступа";
            model.RequiresPassword = true;
            return View(model);
        }

        return RedirectToAction("TakeTest", new { id = model.TestId });
    }

    [HttpGet]
    public async Task<IActionResult> TakeTest(int id)
    {
        var userId = GetUserId();

        var test = await _context.Tests
            .Include(t => t.Course)
            .Include(t => t.TestElements.OrderBy(e => e.Order))
            .FirstOrDefaultAsync(t => t.Id == id);

        if (test == null)
            return NotFound();

        var existingResult = await _context.StudentResults
            .FirstOrDefaultAsync(sr => sr.UserId == userId && sr.TestId == id);

        int attemptsUsed = existingResult?.AttemptsUsed ?? 0;

        if (attemptsUsed >= test.MaxAttempts)
        {
            TempData["Error"] = "Вы исчерпали все попытки для этого теста";
            return RedirectToAction("Index");
        }

        var elements = test.TestElements.ToList();
        
        // Перемешиваем вопросы если нужно
        if (test.ShuffleQuestions)
        {
            elements = elements.OrderBy(x => Guid.NewGuid()).ToList();
        }

        var model = new TakeTestViewModel
        {
            TestId = test.Id,
            TestTitle = test.Title,
            TestDescription = test.Description,
            CourseName = test.Course?.Title ?? "",
            MaxScore = test.MaxScore,
            AttemptsLeft = test.MaxAttempts - attemptsUsed,
            TimeLimitMinutes = test.TimeLimitMinutes,
            TimeLimitSeconds = test.TimeLimitMinutes * 60,
            OneQuestionPerPage = test.OneQuestionPerPage,
            PreventBackNavigation = test.PreventBackNavigation,
            DetectTabSwitch = test.DetectTabSwitch,
            Instructions = test.Instructions,
            PassingScorePercent = test.PassingScorePercent,
            StartTime = DateTime.Now,
            DisableCopyPaste = test.DisableCopyPaste,
            Questions = elements.Select((e, index) =>
            {
                // ОБЩИЕ options для типов, где это просто список строк
                var options = !string.IsNullOrEmpty(e.OptionsJson)
                    ? e.OptionsJson.Split('|', StringSplitOptions.RemoveEmptyEntries)
                        .Select(o => o.Trim()).ToList()
                    : new List<string>();

                // Перемешиваем ответы если нужно (НО не для Matching – там важен порядок пар)
                if (test.ShuffleAnswers && options.Any() && e.Type != ElementType.Matching)
                {
                    options = options.OrderBy(x => Guid.NewGuid()).ToList();
                }

                var vm = new QuestionViewModel
                {
                    ElementId = e.Id,
                    Order = index + 1,
                    QuestionText = e.QuestionText,
                    Type = e.Type,
                    Options = options,
                    Hint = e.Hint,
                    ImageUrl = e.ImageUrl,
                    TimeLimit = e.TimeLimit,
                    AllowPartialCredit = e.AllowPartialCredit
                };

                // ===== MATCHING =====
                if (e.Type == ElementType.Matching)
                {
                    // e.OptionsJson храним как строки "Левый:Правый|Левый2:Правый2|..."
                    var rawPairs = !string.IsNullOrEmpty(e.OptionsJson)
                        ? e.OptionsJson.Split('|', StringSplitOptions.RemoveEmptyEntries)
                        : Array.Empty<string>();

                    var pairs = rawPairs
                        .Select(p => p.Split(':', 2))
                        .Where(p => p.Length == 2)
                        .ToList();

                    vm.LeftItems = pairs.Select(p => p[0].Trim()).ToList();
                    vm.RightItems = pairs.Select(p => p[1].Trim()).ToList();
                }

                // ===== ORDERING =====
                if (e.Type == ElementType.Ordering)
                {
                    vm.ItemsToOrder = options;
                }

                // ===== FILL BLANKS =====
                if (e.Type == ElementType.FillBlanks)
                {
                    vm.FillBlanksText = e.QuestionText;
                    vm.WordsForBlanks = GetShuffledWordsForBlanks(e.CorrectAnswer, e.OptionsJson, test.ShuffleAnswers);
                    vm.BlanksCount = e.CorrectAnswer
                        .Split('|', StringSplitOptions.RemoveEmptyEntries)
                        .Length;
                }
                // ===== TEXT INPUT с несколькими полями =====
                if (e.Type == ElementType.TextInput)
                {
                    // Если в тексте есть [[BLANK]], считаем количество пропусков
                    if (!string.IsNullOrEmpty(e.QuestionText) && e.QuestionText.Contains("[[BLANK]]"))
                    {
                        vm.FillBlanksText = e.QuestionText;
                        vm.BlanksCount = e.QuestionText
                            .Split("[[BLANK]]", StringSplitOptions.None)
                            .Length - 1;
                    }
                }
                return vm;
            }).ToList()
        };

        // Сохраняем время начала в сессию
        HttpContext.Session.SetString($"TestStart_{id}", DateTime.Now.ToString("O"));

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitTest(int testId, Dictionary<string, string> answers, 
        int timeSpentSeconds = 0, int tabSwitchCount = 0)
    {
        var userId = GetUserId();

        var test = await _context.Tests
            .Include(t => t.TestElements)
            .FirstOrDefaultAsync(t => t.Id == testId);

        if (test == null)
            return NotFound();

        var existingResult = await _context.StudentResults
            .Include(r => r.Attempts)
            .FirstOrDefaultAsync(sr => sr.UserId == userId && sr.TestId == testId);

        int attemptsUsed = existingResult?.AttemptsUsed ?? 0;

        if (attemptsUsed >= test.MaxAttempts)
        {
            TempData["Error"] = "Вы исчерпали все попытки";
            return RedirectToAction("Index");
        }

        // Парсим ответы
        var parsedAnswers = new Dictionary<int, string>();
        var parsedMultiAnswers = new Dictionary<int, List<string>>();

        // временное хранилище для многоблочных текстовых ответов (TextInput с [[BLANK]])
        var textBlanks = new Dictionary<int, SortedDictionary<int, string>>();

        foreach (var answer in answers)
        {
            if (answer.Key.StartsWith("multi_"))
            {
                // как было для чекбоксов
                var elementId = int.Parse(answer.Key.Replace("multi_", "").Split('_')[0]);
                if (!parsedMultiAnswers.ContainsKey(elementId))
                    parsedMultiAnswers[elementId] = new List<string>();
                if (!string.IsNullOrEmpty(answer.Value))
                    parsedMultiAnswers[elementId].Add(answer.Value);
            }
            else if (answer.Key.Contains('_'))
            {
                // формат "123_0", "123_1" — отдельные поля TextInput с несколькими пропусками
                var parts = answer.Key.Split('_', 2);
                if (int.TryParse(parts[0], out var elementId) &&
                    int.TryParse(parts[1], out var index))
                {
                    if (!textBlanks.TryGetValue(elementId, out var dict))
                    {
                        dict = new SortedDictionary<int, string>();
                        textBlanks[elementId] = dict;
                    }

                    dict[index] = answer.Value ?? "";
                }
            }
            else
            {
                // одиночные ответы: ключ — это просто Id элемента, например "5"
                if (int.TryParse(answer.Key, out var elementId))
                {
                    parsedAnswers[elementId] = answer.Value ?? "";
                }
            }
        }

        // Склеиваем многоблочные текстовые ответы в строку через |
        foreach (var kv in textBlanks)
        {
            var elementId = kv.Key;
            var dict = kv.Value;

            var joined = string.Join('|', dict.OrderBy(p => p.Key).Select(p => p.Value ?? ""));
            parsedAnswers[elementId] = joined;
        }

        var gradingResult = _gradingService.CalculateDetailedScore(test, parsedAnswers, parsedMultiAnswers);
        var score = gradingResult.TotalScore;
        var scorePercent = test.MaxScore > 0 ? (score / test.MaxScore * 100) : 0;
        bool isPassed;

        if (test.PassingScorePercent <= 0)
        {
            // порог не задан – считаем, что тест можно только «пройти»
            isPassed = true;
        }
        else
        {
            isPassed = scorePercent >= test.PassingScorePercent;
        }

        bool isNewBest = false;

        // Рассчитываем время
        var startTimeStr = HttpContext.Session.GetString($"TestStart_{testId}");
        if (!string.IsNullOrEmpty(startTimeStr) && DateTime.TryParse(startTimeStr, out var startTime))
        {
            timeSpentSeconds = (int)(DateTime.Now - startTime).TotalSeconds;
        }

        // формируем общую «подозрительность» по нескольким критериям
        var suspiciousReasons = new List<string>();
        var isSuspicious = false;

        // 1) вкладки
        if (test.DetectTabSwitch && tabSwitchCount > 0)
        {
            isSuspicious = true;
            suspiciousReasons.Add($"Переключения вкладок: {tabSwitchCount}");
        }

        // 2) время выполнения
        var maxTimeSeconds = test.TimeLimitMinutes * 60;
        if (maxTimeSeconds > 0)
        {
            var ratio = (double)timeSpentSeconds / maxTimeSeconds;
            // пример: меньше 20% от лимита при тесте хотя бы на 10 минут
            if (ratio < 0.2 && test.TimeLimitMinutes >= 10)
            {
                isSuspicious = true;
                suspiciousReasons.Add(
                    $"Подозрительное время выполнения: {timeSpentSeconds} сек при лимите {maxTimeSeconds} сек");
            }
        }

        var attempt = new TestAttempt
        {
            AttemptNumber = attemptsUsed + 1,
            Score = score,
            CorrectAnswers = gradingResult.CorrectCount,
            TotalQuestions = test.TestElements.Count,
            TimeSpentSeconds = timeSpentSeconds,
            StartedAt = DateTime.Now.AddSeconds(-timeSpentSeconds),
            CompletedAt = DateTime.Now,
            AnswersJson = JsonSerializer.Serialize(parsedAnswers),
            TabSwitchCount = tabSwitchCount,
            IsSuspicious = isSuspicious,
            SuspiciousReason = suspiciousReasons.Any()
                ? string.Join("; ", suspiciousReasons)
                : null
        };

        if (existingResult == null)
        {
            existingResult = new StudentResult
            {
                UserId = userId,
                TestId = testId,
                BestScore = score,
                LastScore = score,
                AttemptsUsed = 1,
                IsCompleted = true,
                IsPassed = isPassed,
                TotalTimeSeconds = timeSpentSeconds,
                FirstAttemptAt = DateTime.Now,
                LastAttemptAt = DateTime.Now
            };
            attempt.StudentResultId = existingResult.Id;
            existingResult.Attempts.Add(attempt);
            _context.StudentResults.Add(existingResult);
            isNewBest = true;
        }
        else
        {
            existingResult.AttemptsUsed++;
            existingResult.LastScore = score;
            existingResult.LastAttemptAt = DateTime.Now;
            existingResult.IsCompleted = true;
            existingResult.TotalTimeSeconds += timeSpentSeconds;

            if (score > existingResult.BestScore)
            {
                existingResult.BestScore = score;
                isNewBest = true;
            }
            
            if (isPassed && !existingResult.IsPassed)
            {
                existingResult.IsPassed = true;
            }

            attempt.StudentResultId = existingResult.Id;
            existingResult.Attempts.Add(attempt);
        }

        await _context.SaveChangesAsync();

        // Очищаем сессию
        HttpContext.Session.Remove($"TestStart_{testId}");

        var resultModel = new TestResultSummaryViewModel
        {
            TestId = test.Id,
            TestTitle = test.Title,
            Score = score,
            MaxScore = test.MaxScore,
            ScorePercent = test.MaxScore > 0 ? Math.Round(score / test.MaxScore * 100, 1) : 0,
            CorrectAnswers = gradingResult.CorrectCount,
            PartialAnswers = gradingResult.PartialCount,
            WrongAnswers = gradingResult.WrongCount,
            TotalQuestions = test.TestElements.Count,
            IsNewBestScore = isNewBest,
            IsPassed = isPassed,
            PassingScorePercent = test.PassingScorePercent,
            TimeSpentSeconds = timeSpentSeconds,
            TimeSpentFormatted = $"{timeSpentSeconds / 60}:{timeSpentSeconds % 60:D2}",
            ShowCorrectAnswers = test.ShowCorrectAnswers,
            AttemptsLeft = test.MaxAttempts - existingResult.AttemptsUsed,
            IsSuspicious = attempt.IsSuspicious,
            TabSwitchCount = attempt.TabSwitchCount,
            AnswerReviews = test.ShowCorrectAnswers
    ? gradingResult.QuestionGrades.Select(g =>
    {
        var element = test.TestElements.First(e => e.Id == g.ElementId);

        string yourAnswer = g.YourAnswer;
        string correctAnswer = g.CorrectAnswer;

        if (element.Type == ElementType.Matching)
        {
            yourAnswer = string.IsNullOrWhiteSpace(g.YourAnswer)
                ? ""
                : FormatMatchingAnswer(element, g.YourAnswer);

            // для корректного ответа используем element.CorrectAnswer,
            // а не g.CorrectAnswer, если у тебя там тоже "1=1|2=2|..."
            correctAnswer = string.IsNullOrWhiteSpace(element.CorrectAnswer)
                ? ""
                : FormatMatchingAnswer(element, element.CorrectAnswer);
        }

        return new AnswerReviewViewModel
        {
            Order = element.Order,
            QuestionText = element.QuestionText,
            Type = element.Type,
            YourAnswer = yourAnswer,
            CorrectAnswer = correctAnswer,
            IsCorrect = g.IsCorrect,
            IsPartial = g.IsPartial,
            PointsEarned = g.PointsEarned,
            MaxPoints = g.MaxPoints,
            Explanation = element.Explanation
        };
    }).OrderBy(r => r.Order).ToList()
    : new List<AnswerReviewViewModel>()
        };

        return View("TestResult", resultModel);
    }

    private string FormatMatchingAnswer(TestElement element, string rawMapping)
    {
        if (string.IsNullOrWhiteSpace(rawMapping) || string.IsNullOrWhiteSpace(element.OptionsJson))
            return "";

        // Пары в OptionsJson: "Левый:Правый|Левый2:Правый2|..."
        var pairs = element.OptionsJson
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split(':', 2))
            .Where(p => p.Length == 2)
            .Select((p, idx) => new
            {
                Index = (idx + 1).ToString(), // "1", "2", ...
                Left = p[0].Trim(),
                Right = p[1].Trim()
            })
            .ToList();

        // rawMapping: "1=3|2=1|..."
        var mappings = rawMapping
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Split('=', 2))
            .Where(m => m.Length == 2)
            .ToList();

        var result = new List<string>();

        foreach (var m in mappings)
        {
            var left = pairs.FirstOrDefault(p => p.Index == m[0]);
            var right = pairs.FirstOrDefault(p => p.Index == m[1]);

            if (left == null || right == null)
                continue;

            // "Левый текст → Правый текст"
            result.Add($"{left.Left} → {right.Right}");
        }

        return result.Any() ? string.Join("; ", result) : "";
    }

    [HttpGet]
    public async Task<IActionResult> History()
    {
        var userId = GetUserId();

        var results = await _context.StudentResults
            .Include(r => r.Test)
            .Include(r => r.Attempts)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.LastAttemptAt)
            .ToListAsync();

        var model = results.Select(r => new TestHistoryViewModel
        {
            TestId = r.TestId,
            TestTitle = r.Test?.Title ?? "",
            BestScore = r.BestScore,
            MaxScore = r.Test?.MaxScore ?? 0,
            Attempts = r.Attempts.OrderByDescending(a => a.AttemptNumber).Select(a => new AttemptViewModel
            {
                AttemptId = a.Id,
                AttemptNumber = a.AttemptNumber,
                Score = a.Score,
                MaxScore = r.Test?.MaxScore ?? 0,
                CorrectAnswers = a.CorrectAnswers,
                TotalQuestions = a.TotalQuestions,
                TimeSpentSeconds = a.TimeSpentSeconds,
                TimeSpentFormatted = $"{a.TimeSpentSeconds / 60}:{a.TimeSpentSeconds % 60:D2}",
                CompletedAt = a.CompletedAt,
                IsBest = a.Score == r.BestScore
            }).ToList()
        }).ToList();

        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> AttemptSummary(int attemptId)
    {
        var userId = GetUserId();

        var attempt = await _context.TestAttempts
            .Include(a => a.StudentResult)
                .ThenInclude(r => r.Test)
            .Include(a => a.StudentResult)
                .ThenInclude(r => r.Test.TestElements)
            .FirstOrDefaultAsync(a => a.Id == attemptId &&
                                      a.StudentResult.UserId == userId);

        if (attempt == null)
            return NotFound();

        var test = attempt.StudentResult.Test;

        // парсим сохранённые ответы
        var parsedAnswers = string.IsNullOrEmpty(attempt.AnswersJson)
            ? new Dictionary<int, string>()
            : JsonSerializer.Deserialize<Dictionary<int, string>>(attempt.AnswersJson)
              ?? new Dictionary<int, string>();

        // если у тебя есть сохранённые multi‑ответы — аналогично их достать;
        // пока передаём пустой словарь
        var gradingResult = _gradingService.CalculateDetailedScore(
            test,
            parsedAnswers,
            new Dictionary<int, List<string>>()
        );

        var isPassed = test.PassingScorePercent > 0 &&
                       (attempt.Score / test.MaxScore * 100) >= test.PassingScorePercent;

        var resultModel = new TestResultSummaryViewModel
        {
            TestId = test.Id,
            TestTitle = test.Title,
            Score = attempt.Score,
            MaxScore = test.MaxScore,
            ScorePercent = test.MaxScore > 0
                ? Math.Round(attempt.Score / test.MaxScore * 100, 1)
                : 0,
            CorrectAnswers = attempt.CorrectAnswers,
            PartialAnswers = gradingResult.PartialCount,
            WrongAnswers = gradingResult.WrongCount,
            TotalQuestions = attempt.TotalQuestions,
            IsNewBestScore = false, // просмотр истории, не «новый рекорд»
            IsPassed = isPassed,
            PassingScorePercent = test.PassingScorePercent,
            TimeSpentSeconds = attempt.TimeSpentSeconds,
            TimeSpentFormatted = $"{attempt.TimeSpentSeconds / 60}:{attempt.TimeSpentSeconds % 60:D2}",
            ShowCorrectAnswers = test.ShowCorrectAnswers,
            AttemptsLeft = test.MaxAttempts - attempt.StudentResult.AttemptsUsed,
            IsSuspicious = attempt.IsSuspicious,
            TabSwitchCount = attempt.TabSwitchCount,
            AnswerReviews = test.ShowCorrectAnswers
                ? gradingResult.QuestionGrades
                    .Select(g =>
                    {
                        var element = test.TestElements.First(e => e.Id == g.ElementId);

                        var yourAnswer = g.YourAnswer;
                        var correctAnswer = g.CorrectAnswer;

                        if (element.Type == ElementType.Matching)
                        {
                            yourAnswer = string.IsNullOrWhiteSpace(g.YourAnswer)
                                ? ""
                                : FormatMatchingAnswer(element, g.YourAnswer);

                            correctAnswer = string.IsNullOrWhiteSpace(element.CorrectAnswer)
                                ? ""
                                : FormatMatchingAnswer(element, element.CorrectAnswer);
                        }

                        return new AnswerReviewViewModel
                        {
                            Order = element.Order,
                            QuestionText = element.QuestionText,
                            Type = element.Type,
                            YourAnswer = yourAnswer,
                            CorrectAnswer = correctAnswer,
                            IsCorrect = g.IsCorrect,
                            IsPartial = g.IsPartial,
                            PointsEarned = g.PointsEarned,
                            MaxPoints = g.MaxPoints,
                            Explanation = element.Explanation
                        };
                    })
                    .OrderBy(r => r.Order)
                    .ToList()
                : new List<AnswerReviewViewModel>()
        };

        return View("TestResult", resultModel);
    }
    /// <summary>
    /// Формирует перемешанный список слов для FillBlanks вопроса
    /// </summary>
    private List<string> GetShuffledWordsForBlanks(string correctAnswer, string? optionsJson, bool shuffle)
    {
        var words = new List<string>();
        
        // Добавляем правильные ответы
        var correctWords = correctAnswer
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            .ToList();
        words.AddRange(correctWords);
        
        // Добавляем дистракторы из OptionsJson (если есть)
        if (!string.IsNullOrEmpty(optionsJson))
        {
            var distractors = optionsJson
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => !correctWords.Contains(w, StringComparer.OrdinalIgnoreCase))
                .ToList();
            words.AddRange(distractors);
        }
        
        // Перемешиваем если нужно
        if (shuffle)
        {
            words = words.OrderBy(x => Guid.NewGuid()).ToList();
        }
        
        return words;
    }
}
