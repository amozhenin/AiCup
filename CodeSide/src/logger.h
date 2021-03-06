#ifndef CODESIDE_LOGGER_H
#define CODESIDE_LOGGER_H

#include <chrono>
#include <map>
#include <sstream>
#include <iostream>
#include <cstring>

#define M_TIME_LOGS 1

#ifdef DEBUG
#define M_LOGS 1
#endif

enum ELoggerAction {
    LA_ALL,
    LA_DO_TICK,
    LA_DIJKSTRA,
    LA_FINDPATH,
    LA_SHOT_STRAT,
    LA_DODGE,
    LA_DODGE_OPP,

    LA_ACTIONS_COUNT
};

int djAll = 0, djIn = 0, djJumpAll = 0;

struct TLogger {
    std::vector<std::chrono::system_clock::time_point> _timers;
    int64_t _cumulativeDuration[LA_ACTIONS_COUNT];
    int tick;

    TLogger() {
        memset(_cumulativeDuration, 0, sizeof(_cumulativeDuration));
        tick = 0;
    }

    static TLogger* instance() {
        static TLogger *_instance = nullptr;
        if (_instance == nullptr) {
            _instance = new TLogger();
        }
        return _instance;
    }

    void timerStart() {
        _timers.push_back(std::chrono::system_clock::now());
    }

    int64_t timerGet() {
        auto microseconds = std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::system_clock::now() - _timers.back());
        return microseconds.count();
    }

    int64_t timerEnd() {
        auto res = timerGet();
        _timers.pop_back();
        return res;
    }

    void timerEndLog(const std::string& caption, int limit) {
        auto time = timerEnd() / 1000;
        if (time > limit) {
            log() << tick << "> " << std::string(_timers.size() * 2, '-') << " " << caption << ": " << time << "ms" << std::endl;
        }
    }

    void cumulativeTimerStart(ELoggerAction action) {
        timerStart();
    }

    void cumulativeTimerEnd(ELoggerAction action) {
        _cumulativeDuration[action] += timerEnd();
    }

    std::string getSummary() {
        std::stringstream out;
        out << "[Summary]" << std::endl;
        out << "] ALL                         " << _cumulativeDuration[LA_ALL]                         / 1000 << "ms" << std::endl;
        out << "] DO_TICK                     " << _cumulativeDuration[LA_DO_TICK]                     / 1000 << "ms" << std::endl;
        out << "] DJ                          " << _cumulativeDuration[LA_DIJKSTRA]                    / 1000 << "ms" << std::endl;
        out << "] FINDPATH                    " << _cumulativeDuration[LA_FINDPATH]                    / 1000 << "ms" << std::endl;
        out << "] SHOT_STRAT                  " << _cumulativeDuration[LA_SHOT_STRAT]                  / 1000 << "ms" << std::endl;
        out << "] DODGE                       " << _cumulativeDuration[LA_DODGE]                       / 1000 << "ms" << std::endl;
        out << "] DODGE_OPP                   " << _cumulativeDuration[LA_DODGE_OPP]                   / 1000 << "ms" << std::endl;
        out << "] DJ_IN   " << djIn << std::endl;
        out << "] DJ_ALL  " << djAll << std::endl;
        out << "] DJ_JALL " << djJumpAll << std::endl;
        return out.str();
    }

    std::ostream& log() {
        return std::cout;
    }

    std::ostream& error() {
        return std::cerr;
    }
};

#if M_LOGS
#define LOG(msg) TLogger::instance()->log() << msg << std::endl;
#define LOG_ERROR(msg) TLogger::instance()->error() << msg << std::endl;
#else
#define LOG(msg)
#define LOG_ERROR(msg)
#endif

#if M_TIME_LOGS
#define TIMER_START() TLogger::instance()->timerStart()
#define TIMER_ENG_LOG(caption) TLogger::instance()->timerEndLog((caption), 200)
#define OP_START(action) TLogger::instance()->cumulativeTimerStart(LA_ ## action)
#define OP_END(action) TLogger::instance()->cumulativeTimerEnd(LA_ ## action)
#else
#define TIMER_START()
#define TIMER_ENG_LOG(caption)
#define OP_START(action)
#define OP_END(action)
#endif

#endif //CODESIDE_LOGGER_H
