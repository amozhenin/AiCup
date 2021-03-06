#ifndef CODESIDE_POINT_H
#define CODESIDE_POINT_H

#include <cmath>

#define EPS 1e-9
#define EPS2 (EPS*EPS)
#define SQR(x) ((x)*(x))

struct TPoint {
    double x;
    double y;

    TPoint() {
        x = y = 0;
    }

    explicit TPoint(double x, double y) : x(x), y(y) {
    }

    void set(double x, double y) {
        this->x = x;
        this->y = y;
    }

    [[nodiscard]] double length() const {
        return sqrt(x * x + y * y);
    }

    [[nodiscard]] double length2() const {
        return x * x + y * y;
    }

    // Вектор длины 1 того-же направления, или (0, 0), если вектор нулевой
    [[nodiscard]] TPoint normalized() const {
        auto len = length();
        if (len < EPS)
            len = 1;
        return TPoint(x / len, y / len);
    }

    void normalize() {
        auto len = length();
        if (len > EPS) {
            x /= len;
            y /= len;
        }
    }

    // Скалярное произведение
    double operator *(const TPoint &b) const {
        return x * b.x + y * b.y;
    }

    TPoint operator *(double b) const {
        return TPoint(x * b, y * b);
    }

    TPoint operator /(double b) const {
        return TPoint(x / b, y / b);
    }

    TPoint operator +(const TPoint &b) const {
        return TPoint(x + b.x, y + b.y);
    }

    TPoint operator -(const TPoint &b) const {
        return TPoint(x - b.x, y - b.y);
    }

    TPoint &operator +=(const TPoint &b) {
        x += b.x;
        y += b.y;
        return *this;
    }

    TPoint &operator -=(const TPoint &b) {
        x -= b.x;
        y -= b.y;
        return *this;
    }

    TPoint &operator *=(double b) {
        x *= b;
        y *= b;
        return *this;
    }

    TPoint &operator /=(double b) {
        x /= b;
        y /= b;
        return *this;
    }

    [[nodiscard]] double getAngleTo(const TPoint& to) const {
        return atan2(to.y - y, to.x - x);
    }

    [[nodiscard]] double getAngle() const {
        return atan2(y, x);
    }

    [[nodiscard]] double getDistanceTo(const TPoint& point) const {
        return sqrt(SQR(x - point.x) + SQR(y - point.y));
    }

    [[nodiscard]] double getDistanceTo2(const TPoint& point) const {
        return SQR(x - point.x) + SQR(y - point.y);
    }

    static TPoint byAngle(double angle) {
        return TPoint(cos(angle), sin(angle));
    }

    [[nodiscard]] TPoint rotatedClockwise(double angle) const {
        auto cos = std::cos(angle);
        auto sin = std::sin(angle);
        return TPoint(cos * x + sin * y, -sin * x + cos * y);
    }

    static double getAngleBetween(TPoint vec1, TPoint vec2) {
        return acos(vec1 * vec2 / vec1.length() / vec2.length());
    }

    static double getAngleBetween(double alpha, double beta) {
        alpha = angleNormalize(alpha);
        beta = angleNormalize(beta);
        return std::abs(angleNormalize(alpha - beta));
    }

    static double angleNormalize(double angle) {
        while (angle > M_PI) {
            angle -= 2 * M_PI;
        }
        while (angle < -M_PI) {
            angle += 2 * M_PI;
        }
        return angle;
    }

    bool operator <(const TPoint& point) const {
        if (x != point.x) {
            return x < point.x;
        }
        return y < point.y;
    }
};

#endif //CODESIDE_POINT_H
