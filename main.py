import os
from typing import Optional

from dotenv import load_dotenv
import requests
from werkzeug.security import check_password_hash
from fastapi import FastAPI, HTTPException
from sqlalchemy import create_engine, text
from sqlalchemy.exc import IntegrityError, SQLAlchemyError
from pydantic import BaseModel

load_dotenv()

app = FastAPI(title="StudGov API")

DATABASE_URL = os.getenv("DATABASE_URL")

if not DATABASE_URL:
    raise RuntimeError("DATABASE_URL is not set in .env file")

TELEGRAM_TOKEN = os.getenv("TELEGRAM_TOKEN")

engine = create_engine(DATABASE_URL, pool_pre_ping=True)

def send_telegram_message(telegram_id: int, message: str):
    if not TELEGRAM_TOKEN:
        return

    url = f"https://api.telegram.org/bot{TELEGRAM_TOKEN}/sendMessage"

    try:
        requests.post(
            url,
            json={
                "chat_id": telegram_id,
                "text": message
            },
            timeout=5
        )
    except Exception:
        pass

class NewsCreate(BaseModel):
    title: str
    body: str
    author_user_id: int

class NewsUpdate(BaseModel):
    title: str
    body: str
    is_published: bool = True   
    updated_by_user_id: Optional[int] = None 

class LoginRequest(BaseModel):
    username: str
    password: str

class EventCreate(BaseModel):
    title: str
    starts_at: str
    created_by_user_id: int
    description: Optional[str] = None
    location: Optional[str] = None
    capacity: Optional[int] = None
    requires_registration: bool = True

class EventUpdate(BaseModel):
    title: str
    starts_at: str
    description: Optional[str] = None
    location: Optional[str] = None
    capacity: Optional[int] = None
    requires_registration: bool = True
    is_active: bool = True    
    updated_by_user_id: Optional[int] = None


class EventRegistrationCreate(BaseModel):
    user_id: int

class RequestCreate(BaseModel):
    user_id: Optional[int] = None
    is_anonymous: bool = False
    category: Optional[str] = None
    text: str


class RequestUpdate(BaseModel):
    status: str
    answer_text: Optional[str] = None
    handled_by_user_id: Optional[int] = None

class ActivityCreate(BaseModel):
    user_id: int
    event_id: Optional[int] = None
    points: int = 1
    reason: Optional[str] = None
    created_by_user_id: Optional[int] = None    

class TelegramRegisterRequest(BaseModel):
    telegram_id: int
    full_name: str
    group_name: str
    phone_number: str

class UserUpdate(BaseModel):
    role_id: Optional[int] = None
    is_active: Optional[bool] = None
    updated_by_user_id: Optional[int] = None    

class SystemLogCreate(BaseModel):
    user_id: Optional[int] = None
    action: str
    details: Optional[str] = None    

def add_system_log(user_id: Optional[int], action: str, details: Optional[str] = None):
    query = text("""
        INSERT INTO system_logs (user_id, action, details)
        VALUES (:user_id, :action, :details)
    """)

    with engine.begin() as conn:
        conn.execute(query, {
            "user_id": user_id,
            "action": action,
            "details": details
        })    


@app.get("/")
def root():
    return {
        "status": "ok",
        "message": "StudGov API is running"
    }


@app.get("/news")
def get_news():
    query = text("""
        SELECT id, title, body, created_at, is_published
        FROM news
        ORDER BY created_at DESC
        LIMIT 50
    """)

    with engine.connect() as conn:
        rows = conn.execute(query).mappings().all()
        return [dict(row) for row in rows]


@app.post("/news")
def create_news(news: NewsCreate):
    query = text("""
        INSERT INTO news (title, body, author_user_id)
        VALUES (:title, :body, :author_user_id)
        RETURNING id, title, body, author_user_id, created_at, is_published
    """)

    with engine.begin() as conn:
        result = conn.execute(query, {
            "title": news.title,
            "body": news.body,
            "author_user_id": news.author_user_id
        })

        new_row = result.mappings().first()

    add_system_log(
        news.author_user_id,
        "Створення новини",
        f"Створено новину: {news.title}"
    )

    return dict(new_row)

@app.patch("/news/{news_id}")
def update_news(news_id: int, news: NewsUpdate):
    query = text("""
        UPDATE news
        SET title = :title,
            body = :body,
            is_published = :is_published
        WHERE id = :news_id
        RETURNING id, title, body, created_at, is_published
    """)

    with engine.begin() as conn:
        result = conn.execute(query, {
            "news_id": news_id,
            "title": news.title,
            "body": news.body,
            "is_published": news.is_published
        })

        updated_row = result.mappings().first()

        if not updated_row:
            raise HTTPException(status_code=404, detail="Новину не знайдено")
        
    updated_by_user_id: Optional[int] = None

    add_system_log(
            news.updated_by_user_id,
            "Редагування новини",
            f"Оновлено новину ID {news_id}: {news.title}"
        )

    return dict(updated_row)

@app.delete("/news/{news_id}")
def delete_news(news_id: int, user_id: Optional[int] = None):
    query = text("""
        DELETE FROM news
        WHERE id = :news_id
        RETURNING id, title
    """)

    with engine.begin() as conn:
        result = conn.execute(query, {"news_id": news_id})
        deleted_row = result.mappings().first()

        if not deleted_row:
            raise HTTPException(status_code=404, detail="Новину не знайдено")

    add_system_log(
        user_id,
        "Видалення новини",
        f"Видалено новину ID {news_id}: {deleted_row['title']}"
    )

    return {
        "message": "Новину успішно видалено",
        "deleted_news": dict(deleted_row)
    }

@app.get("/events")
def get_events():
    query = text("""
        SELECT id, title, description, starts_at, location, capacity, requires_registration, created_at, is_active
        FROM events
        ORDER BY starts_at ASC
        LIMIT 50
    """)

    with engine.connect() as conn:
        rows = conn.execute(query).mappings().all()
        return [dict(row) for row in rows]


@app.post("/events")
def create_event(event: EventCreate):
    query = text("""
        INSERT INTO events (
            title,
            description,
            starts_at,
            location,
            capacity,
            requires_registration,
            created_by_user_id
        )
        VALUES (
            :title,
            :description,
            :starts_at,
            :location,
            :capacity,
            :requires_registration,
            :created_by_user_id
        )
        RETURNING 
            id,
            title,
            description,
            starts_at,
            location,
            capacity,
            requires_registration,
            created_by_user_id,
            created_at,
            is_active
    """)

    with engine.begin() as conn:
        result = conn.execute(query, {
            "title": event.title,
            "description": event.description,
            "starts_at": event.starts_at,
            "location": event.location,
            "capacity": event.capacity,
            "requires_registration": event.requires_registration,
            "created_by_user_id": event.created_by_user_id
        })

    new_row = result.mappings().first()

    add_system_log(
        event.created_by_user_id,
        "Створення події",
        f"Створено подію: {event.title}"
    )

    return dict(new_row)

@app.patch("/events/{event_id}")
def update_event(event_id: int, event: EventUpdate):
    query = text("""
        UPDATE events
        SET title = :title,
            description = :description,
            starts_at = :starts_at,
            location = :location,
            capacity = :capacity,
            requires_registration = :requires_registration,
            is_active = :is_active
        WHERE id = :event_id
        RETURNING 
            id,
            title,
            description,
            starts_at,
            location,
            capacity,
            requires_registration,
            created_at,
            is_active
    """)

    with engine.begin() as conn:
        result = conn.execute(query, {
            "event_id": event_id,
            "title": event.title,
            "description": event.description,
            "starts_at": event.starts_at,
            "location": event.location,
            "capacity": event.capacity,
            "requires_registration": event.requires_registration,
            "is_active": event.is_active
        })

        updated_row = result.mappings().first()

        if not updated_row:
            raise HTTPException(status_code=404, detail="Подію не знайдено")

    add_system_log(
        None,
        "Редагування події",
        f"Оновлено подію ID {event_id}: {event.title}"
    )

    return dict(updated_row)


@app.delete("/events/{event_id}")
def delete_event(event_id: int):
    query = text("""
        DELETE FROM events
        WHERE id = :event_id
        RETURNING id, title
    """)

    with engine.begin() as conn:
        result = conn.execute(query, {"event_id": event_id})
        deleted_row = result.mappings().first()

        if not deleted_row:
            raise HTTPException(status_code=404, detail="Подію не знайдено")

    add_system_log(
        None,
        "Видалення події",
        f"Видалено подію ID {event_id}: {deleted_row['title']}"
    )

    return {
        "message": "Подію успішно видалено",
        "deleted_event": dict(deleted_row)
    }

@app.get("/events/{event_id}/registrations")
def get_event_registrations(event_id: int):
    query = text("""
        SELECT 
            er.id AS registration_id,
            er.event_id,
            er.user_id,
            u.full_name,
            u.group_name,
            u.phone_number,
            er.registered_at,
            er.status
        FROM event_registrations er
        JOIN users u ON u.id = er.user_id
        WHERE er.event_id = :event_id
        ORDER BY u.full_name
    """)

    with engine.connect() as conn:
        rows = conn.execute(query, {"event_id": event_id}).mappings().all()
        return [dict(row) for row in rows]

@app.post("/events/{event_id}/register")
def register_for_event(event_id: int, registration: EventRegistrationCreate):
    try:
        with engine.begin() as conn:
            event_query = text("""
                SELECT id, title, capacity, requires_registration, is_active
                FROM events
                WHERE id = :event_id
                LIMIT 1
            """)

            event = conn.execute(
                event_query,
                {"event_id": event_id}
            ).mappings().first()

            if not event:
                raise HTTPException(
                    status_code=404,
                    detail="Подію не знайдено"
                )

            if not event["is_active"]:
                raise HTTPException(
                    status_code=400,
                    detail="Подія неактивна"
                )

            if not event["requires_registration"]:
                raise HTTPException(
                    status_code=400,
                    detail="Для цієї події реєстрація не потрібна"
                )

            count_query = text("""
                SELECT COUNT(*) AS registered_count
                FROM event_registrations
                WHERE event_id = :event_id
            """)

            count_result = conn.execute(
                count_query,
                {"event_id": event_id}
            ).mappings().first()

            registered_count = count_result["registered_count"]

            if event["capacity"] is not None and registered_count >= event["capacity"]:
                raise HTTPException(
                    status_code=400,
                    detail="Кількість місць на подію вичерпано"
                )

            insert_query = text("""
                INSERT INTO event_registrations (event_id, user_id)
                VALUES (:event_id, :user_id)
                RETURNING id, event_id, user_id, registered_at, status
            """)

            result = conn.execute(insert_query, {
                "event_id": event_id,
                "user_id": registration.user_id
            })

            new_row = result.mappings().first()
            return dict(new_row)

    except IntegrityError:
        raise HTTPException(
            status_code=400,
            detail="Ви вже зареєстровані на цю подію"
        )

    except HTTPException:
        raise

    except SQLAlchemyError:
        raise HTTPException(
            status_code=500,
            detail="Помилка бази даних під час реєстрації"
        )
    
@app.post("/requests")
def create_request(request_data: RequestCreate):
    query = text("""
        INSERT INTO requests (user_id, is_anonymous, category, text)
        VALUES (:user_id, :is_anonymous, :category, :text)
        RETURNING id, user_id, is_anonymous, category, text, status, created_at, updated_at
    """)

    with engine.begin() as conn:
        result = conn.execute(query, {
            "user_id": request_data.user_id,
            "is_anonymous": request_data.is_anonymous,
            "category": request_data.category,
            "text": request_data.text
        })
        new_row = result.mappings().first()
        add_system_log(
            None,
            "Створення звернення",
            f"Створено звернення: {request_data.text[:50]}"
        )
        return dict(new_row)


@app.get("/requests")
def get_requests():
    query = text("""
        SELECT 
            r.id,
            r.user_id,
            u.full_name,
            u.group_name,
            u.phone_number,
            r.is_anonymous,
            r.category,
            r.text,
            r.status,
            r.answer_text,
            r.handled_by_user_id,
            r.created_at,
            r.updated_at
        FROM requests r
        LEFT JOIN users u ON u.id = r.user_id
        ORDER BY r.created_at DESC
    """)

    with engine.connect() as conn:
        rows = conn.execute(query).mappings().all()
        return [dict(row) for row in rows]


@app.patch("/requests/{request_id}")
def update_request(request_id: int, request_data: RequestUpdate):
    query = text("""
        UPDATE requests
        SET status = :status,
            answer_text = :answer_text,
            handled_by_user_id = :handled_by_user_id,
            updated_at = NOW()
        WHERE id = :request_id
        RETURNING id, user_id, is_anonymous, category, text, status, answer_text,
                  handled_by_user_id, created_at, updated_at
    """)

    with engine.begin() as conn:
        result = conn.execute(query, {
            "request_id": request_id,
            "status": request_data.status,
            "answer_text": request_data.answer_text,
            "handled_by_user_id": request_data.handled_by_user_id
        })

        updated_row = result.mappings().first()

        if not updated_row:
            raise HTTPException(status_code=404, detail="Звернення не знайдено")

        # Якщо звернення неанонімне і є відповідь — шукаємо Telegram ID студента
        if (
            updated_row["user_id"] is not None
            and not updated_row["is_anonymous"]
            and updated_row["answer_text"]
        ):
            user_query = text("""
                SELECT telegram_id
                FROM users
                WHERE id = :user_id
                LIMIT 1
            """)

            user = conn.execute(
                user_query,
                {"user_id": updated_row["user_id"]}
            ).mappings().first()

            if user and user["telegram_id"]:
                message = (
                    "📬 Відповідь на ваше звернення\n\n"
                    f"📝 Ваше звернення:\n{updated_row['text']}\n\n"
                    f"✅ Відповідь:\n{updated_row['answer_text']}"
                )

                send_telegram_message(user["telegram_id"], message)

    add_system_log(
        request_data.handled_by_user_id,
        "Оновлення звернення",
        f"Оновлено звернення ID {request_id}"
    )

    return dict(updated_row)
    
@app.post("/activity")
def create_activity(activity: ActivityCreate):
    query = text("""
        INSERT INTO activity_points (user_id, event_id, points, reason, created_by_user_id)
        VALUES (:user_id, :event_id, :points, :reason, :created_by_user_id)
        RETURNING id, user_id, event_id, points, reason, created_by_user_id, created_at
    """)

    with engine.begin() as conn:
        result = conn.execute(query, {
            "user_id": activity.user_id,
            "event_id": activity.event_id,
            "points": activity.points,
            "reason": activity.reason,
            "created_by_user_id": activity.created_by_user_id
        })

        new_row = result.mappings().first()

    add_system_log(
        activity.created_by_user_id,
        "Нарахування балів",
        f"Студенту ID {activity.user_id} нараховано {activity.points} бал(ів)"
    )

    return dict(new_row)


@app.get("/activity")
def get_activity():
    query = text("""
        SELECT 
            ap.id,
            ap.user_id,
            student.full_name AS student_name,
            ap.event_id,
            e.title AS event_title,
            ap.points,
            ap.reason,
            ap.created_by_user_id,
            admin.full_name AS created_by_name,
            ap.created_at
        FROM activity_points ap
        LEFT JOIN users student ON student.id = ap.user_id
        LEFT JOIN events e ON e.id = ap.event_id
        LEFT JOIN users admin ON admin.id = ap.created_by_user_id
        ORDER BY ap.created_at DESC
    """)

    with engine.connect() as conn:
        rows = conn.execute(query).mappings().all()
        return [dict(row) for row in rows]


@app.get("/users/{user_id}/activity")
def get_user_activity(user_id: int):
    query = text("""
        SELECT id, user_id, event_id, points, reason, created_by_user_id, created_at
        FROM activity_points
        WHERE user_id = :user_id
        ORDER BY created_at DESC
    """)

    summary_query = text("""
        SELECT COALESCE(SUM(points), 0) AS total_points
        FROM activity_points
        WHERE user_id = :user_id
    """)

    with engine.connect() as conn:
        rows = conn.execute(query, {"user_id": user_id}).mappings().all()
        total = conn.execute(summary_query, {"user_id": user_id}).mappings().first()

        return {
            "user_id": user_id,
            "total_points": total["total_points"],
            "history": [dict(row) for row in rows]
        }

@app.get("/users/by-telegram/{telegram_id}/activity")
def get_user_activity_by_telegram(telegram_id: int):
    user_query = text("""
        SELECT id, full_name, telegram_id
        FROM users
        WHERE telegram_id = :telegram_id
        LIMIT 1
    """)

    activity_query = text("""
        SELECT id, user_id, event_id, points, reason, created_by_user_id, created_at
        FROM activity_points
        WHERE user_id = :user_id
        ORDER BY created_at DESC
    """)

    summary_query = text("""
        SELECT COALESCE(SUM(points), 0) AS total_points
        FROM activity_points
        WHERE user_id = :user_id
    """)

    with engine.connect() as conn:
        user = conn.execute(user_query, {"telegram_id": telegram_id}).mappings().first()

        if not user:
            return {
                "found": False,
                "message": "Користувача з таким telegram_id не знайдено"
            }

        user_id = user["id"]

        history_rows = conn.execute(activity_query, {"user_id": user_id}).mappings().all()
        total = conn.execute(summary_query, {"user_id": user_id}).mappings().first()

        return {
            "found": True,
            "user_id": user_id,
            "full_name": user["full_name"],
            "telegram_id": user["telegram_id"],
            "total_points": total["total_points"],
            "history": [dict(row) for row in history_rows]
        }

@app.get("/users")
def get_users():
    query = text("""
        SELECT 
            u.id,
            u.username,
            u.full_name,
            u.telegram_id,
            u.is_active,
            u.created_at,
            r.code AS role_code,
            r.name AS role_name
        FROM users u
        JOIN roles r ON r.id = u.role_id
        ORDER BY u.id
    """)

    with engine.connect() as conn:
        rows = conn.execute(query).mappings().all()
        return [dict(row) for row in rows]
    
@app.post("/login")
def login(data: LoginRequest):
    query = text("""
        SELECT 
            u.id,
            u.username,
            u.password_hash,
            u.full_name,
            u.is_active,
            r.code AS role_code,
            r.name AS role_name
        FROM users u
        JOIN roles r ON r.id = u.role_id
        WHERE u.username = :username
        LIMIT 1
    """)

    with engine.connect() as conn:
        user = conn.execute(query, {"username": data.username}).mappings().first()

        if not user:
            raise HTTPException(status_code=401, detail="Невірний логін або пароль")

        if not user["is_active"]:
            raise HTTPException(status_code=403, detail="Користувач неактивний")

        if not check_password_hash(
            user["password_hash"],
            data.password
        ):
            raise HTTPException(
            status_code=401,
            detail="Невірний логін або пароль"
            )

        return {
            "id": user["id"],
            "username": user["username"],
            "full_name": user["full_name"],
            "role_code": user["role_code"],
            "role_name": user["role_name"]
        }           
    
@app.post("/telegram/register")
def register_telegram_user(data: TelegramRegisterRequest):

    existing_query = text("""
        SELECT id
        FROM users
        WHERE telegram_id = :telegram_id
        LIMIT 1
    """)

    insert_query = text("""
        INSERT INTO users (
            role_id,
            username,
            telegram_id,
            full_name,
            group_name,
            phone_number,
            is_active
        )
        VALUES (
            1,
            :username,
            :telegram_id,
            :full_name,
            :group_name,
            :phone_number,
            true
        )
        RETURNING 
            id,
            telegram_id,
            full_name,
            group_name,
            phone_number,
            is_active
    """)

    with engine.begin() as conn:

        existing_user = conn.execute(
            existing_query,
            {"telegram_id": data.telegram_id}
        ).mappings().first()

        if existing_user:
            raise HTTPException(
                status_code=400,
                detail="Користувач уже зареєстрований"
            )

        result = conn.execute(insert_query, {
            "username": f"tg_{data.telegram_id}",
            "telegram_id": data.telegram_id,
            "full_name": data.full_name,
            "group_name": data.group_name,
            "phone_number": data.phone_number
        })

        new_user = result.mappings().first()

        return dict(new_user)    
    
@app.get("/system-logs")
def get_system_logs():
    query = text("""
        SELECT 
            l.id,
            l.user_id,
            u.full_name,
            l.action,
            l.details,
            l.created_at
        FROM system_logs l
        LEFT JOIN users u ON u.id = l.user_id
        ORDER BY l.created_at DESC
        LIMIT 100
    """)

    with engine.connect() as conn:
        rows = conn.execute(query).mappings().all()
        return [dict(row) for row in rows]        
    
@app.get("/roles")
def get_roles():
    query = text("""
        SELECT id, code, name
        FROM roles
        ORDER BY id
    """)

    with engine.connect() as conn:
        rows = conn.execute(query).mappings().all()
        return [dict(row) for row in rows]

@app.patch("/users/{user_id}")
def update_user(user_id: int, user_data: UserUpdate):
    query = text("""
        UPDATE users
        SET role_id = COALESCE(:role_id, role_id),
            is_active = COALESCE(:is_active, is_active)
        WHERE id = :user_id
        RETURNING id, username, full_name, telegram_id, is_active, created_at, role_id
    """)

    with engine.begin() as conn:
        result = conn.execute(query, {
            "user_id": user_id,
            "role_id": user_data.role_id,
            "is_active": user_data.is_active
        })

        updated_user = result.mappings().first()

        if not updated_user:
            raise HTTPException(status_code=404, detail="Користувача не знайдено")

    add_system_log(
        user_data.updated_by_user_id,
        "Оновлення облікового запису",
        f"Оновлено користувача ID {user_id}"
    )

    return dict(updated_user)        